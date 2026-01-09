using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class ProjectSummaryAiResult
    {
        public string Summary { get; set; } = "";
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IProjectSummaryAiService
    {
        Task<ProjectSummaryAiResult> GenerateProjectSummaryAsync(int projectId);
    }

    public class GoogleProjectSummaryAiService : IProjectSummaryAiService
    {
        private readonly ApplicationDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GoogleProjectSummaryAiService> _logger;

        private const string ModelName = "gemini-2.5-flash-lite";
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

        public GoogleProjectSummaryAiService(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<GoogleProjectSummaryAiService> logger)
        {
            _db = db;
            _logger = logger;

            // imi fac un HttpClient simplu doar pentru apelul catre Gemini
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // iau cheia din appsettings (ideal din appsettings.Development.json ca sa nu ajunga pe git)
            _apiKey = configuration["GoogleAI:ApiKey"]
                ?? throw new Exception("GoogleAI:ApiKey not found in configuration");
        }

        public async Task<ProjectSummaryAiResult> GenerateProjectSummaryAsync(int projectId)
        {
            try
            {
                // imi iau proiectul impreuna cu task-urile lui
                var project = await _db.Projects
                    .Include(p => p.TaskItems)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                // daca nu am proiect sau nu am task-uri, afisez un mesaj simplu
                if (project == null || project.TaskItems.Count == 0)
                {
                    return new ProjectSummaryAiResult
                    {
                        Success = true,
                        Summary = "<p>🫥 <strong>No recent updates</strong> for this project.</p>"
                    };
                }

                // calculez statisticile aici, ca sa nu las modelul sa inventeze procente
                var total = project.TaskItems.Count;

                var completed = project.TaskItems.Count(t => t.Status.ToString() == "Completed");
                var inProgress = project.TaskItems.Count(t => t.Status.ToString() == "InProgress");
                var notStarted = project.TaskItems.Count(t => t.Status.ToString() == "NotStarted");

                var progressPercent = total == 0
                    ? 0
                    : (int)Math.Round((completed * 100.0) / total);

                var now = DateTime.Now;

                var overdueCount = project.TaskItems.Count(t =>
                    t.EndDate < now && t.Status.ToString() != "Completed");

                var nextDeadlineTask = project.TaskItems
                    .Where(t => t.EndDate >= now)
                    .OrderBy(t => t.EndDate)
                    .FirstOrDefault();

                var nextDeadlineText = nextDeadlineTask == null
                    ? "None"
                    : $"{nextDeadlineTask.Title} ({nextDeadlineTask.EndDate:yyyy-MM-dd})";

                // trimit si task-urile ca sa poata mentiona nume concrete, dar pastram totul compact
                var tasksText = string.Join("\n", project.TaskItems.Select(t =>
                    $"- Title: {t.Title} | Status: {t.Status} | Start: {t.StartDate:yyyy-dd-MM} | End: {t.EndDate:yyyy-dd-MM}"
                ));

                // prompt mai liber, dar cu reguli clare ca sa nu sune a conversatie
                var prompt = $@"
You are an AI project assistant generating a dashboard-style project summary.

Create a short summary in English as SIMPLE HTML (no markdown).
Output ONLY HTML.
Allowed tags: <div>, <p>, <strong>, <ul>, <li>, <br>.
No attributes. No links. No images. No scripts. No styles.

Purpose:
This text will be displayed inside a project management dashboard.
It should sound intelligent and helpful, but not conversational

Hard rules:
- Do NOT address the user directly.
- Do NOT greet the user (no 'Hello', 'Hi', etc.).
- Do NOT ask questions.
- Do NOT include phrases like:
  'let me know if you need anything else',
  'feel free to ask',
  'I can help',
  'reach out',
  'happy to help'.
- Do NOT end with a conversational closing.
- End with a project-focused statement or action.

You may vary wording and structure freely, but you MUST include the following factual information somewhere in the text:
- Progress percentage: {progressPercent}%
- Task counts: total {total}, completed {completed}, in progress {inProgress}, not started {notStarted}
- Overdue tasks count: {overdueCount}
- Next deadline: {nextDeadlineText} (or clearly state that there is no upcoming deadline)
- 2 to 4 concrete next steps (actionable, task-related)

Tone and style:
- Professional and confident
- Feels like an AI assistant, not a static report
- Use a few emojis naturally (2–5 total), relevant to the content

Project title:
{project.Title}

Task details:
{tasksText}

Additional guidance:
- If there are overdue tasks, clearly highlight them as an attention point.
- If there is no upcoming deadline, explicitly state this.
- Keep the output compact (around 10–15 lines total).
";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        // temperatura mai mare ca sa fie mai "AI", dar nu haotic
                        temperature = 0.75,
                        maxOutputTokens = 280
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // trimit request-ul catre Gemini
                var response = await _httpClient.PostAsync(
                    $"{BaseUrl}{ModelName}:generateContent?key={_apiKey}",
                    content);

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error: {Status} - {Body}", response.StatusCode, responseText);
                    return new ProjectSummaryAiResult
                    {
                        Success = false,
                        ErrorMessage = "AI request failed"
                    };
                }

                // parsez raspunsul din JSON
                using var doc = JsonDocument.Parse(responseText);
                var summaryHtml = doc
                    .RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                // verific ca nu imi vine html periculos (noi vrem doar tag-uri simple)
                var safeHtml = BasicHtmlSafetyClean(summaryHtml);

                // daca modelul tot scapa formulare "chatty", le elimin ca sa nu para ca vorbeste cu userul
                safeHtml = RemoveChattyClosings(safeHtml);

                // daca cumva a iesit prea scurt, pun un fallback ca sa nu ramana gol in UI
                if (string.IsNullOrWhiteSpace(safeHtml) || safeHtml.Length < 25)
                {
                    safeHtml = FallbackHtml(progressPercent, total, completed, inProgress, notStarted, overdueCount, nextDeadlineText);
                }

                return new ProjectSummaryAiResult
                {
                    Success = true,
                    Summary = safeHtml
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI generation error");
                return new ProjectSummaryAiResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string FallbackHtml(int progressPercent, int total, int completed, int inProgress, int notStarted, int overdueCount, string nextDeadlineText)
        {
            // daca AI-ul esueaza, afisez un rezumat decent, tot cu html simplu
            var riskLine = overdueCount > 0
                ? $"<p>⚠️ <strong>Attention:</strong> {overdueCount} overdue task(s).</p>"
                : "<p>✅ <strong>Risks:</strong> No major risks detected.</p>";

            return $@"
<div>
  <p>📌 <strong>Project summary</strong></p>
  <p>📊 <strong>Progress:</strong> {progressPercent}% (Total: {total}, Completed: {completed}, In progress: {inProgress}, Not started: {notStarted})</p>
  <p>⏳ <strong>Next deadline:</strong> {nextDeadlineText}</p>
  {riskLine}
  <p>✅ <strong>Next steps:</strong></p>
  <ul>
    <li>Review overdue tasks and adjust priorities.</li>
    <li>Move at least one task into In Progress.</li>
  </ul>
</div>".Trim();
        }

        private string BasicHtmlSafetyClean(string html)
        {
            // vreau sa afisez html doar daca respecta regulile: fara scripturi, fara link-uri, fara atribute
            var trimmed = (html ?? "").Trim();
            var lower = trimmed.ToLowerInvariant();

            if (lower.Contains("<script") || lower.Contains("<img") || lower.Contains("<a") || lower.Contains("<style"))
                return "<p>🤖 <strong>AI Summary:</strong> Summary could not be displayed safely.</p>";

            if (lower.Contains("class=") || lower.Contains("style=") || lower.Contains("href=") || lower.Contains("src="))
                return "<p>🤖 <strong>AI Summary:</strong> Summary could not be displayed safely.</p>";

            // daca nu e html, il encodez ca text, ca sa nu fie interpretat ca markup
            if (!trimmed.StartsWith("<"))
                return $"<p>🤖 <strong>AI Summary:</strong> {System.Net.WebUtility.HtmlEncode(trimmed)}</p>";

            return trimmed;
        }

        private string RemoveChattyClosings(string html)
        {
            // aici scot automat fraze tipice de chat care nu se potrivesc intr-un dashboard
            // prefer sa tai linia respectiva decat sa ramana si sa para ciudat
            var result = html ?? "";

            var bannedPhrases = new[]
            {
                "let me know if you need anything else",
                "let me know if you need anything",
                "feel free to ask",
                "i can help",
                "reach out",
                "happy to help",
                "if you need anything else",
                "if you need anything"
            };

            foreach (var phrase in bannedPhrases)
            {
                result = result.Replace(phrase, "", StringComparison.OrdinalIgnoreCase);
            }

            // mai curat spatii duble care pot ramane dupa replace
            while (result.Contains("  "))
                result = result.Replace("  ", " ");

            return result.Trim();
        }
    }
}
