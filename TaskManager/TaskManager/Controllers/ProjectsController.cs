using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.Projects;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectSummaryAiService _projectSummaryAiService;

        public ProjectsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IProjectSummaryAiService projectSummaryAiService)
        {
            _db = db;
            _userManager = userManager;
            _projectSummaryAiService = projectSummaryAiService;
        }
        public IActionResult Index()
        {
            return RedirectToAction(nameof(My));
        }


        // GET: /Projects/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new ProjectCreateVm());
        }

        // POST: /Projects/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProjectCreateVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = new Project
            {
                Title = vm.Title.Trim(),
                Description = vm.Description.Trim(),
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                OrganizerId = userId
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            // Il adaug pe creator automat ca membru in proiect
            // (ne ajuta la listare + logica de "is member" mai tarziu)
            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId
            });
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(My));
        }

        // GET: /Projects/My
        [HttpGet]
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var projects = await _db.Projects
                .AsNoTracking()
                // Aici afisez doar proiectele active, ca cele sterse (IsActive=false) sa nu mai apara deloc in lista
                .Where(p => p.IsActive)
                .Where(p =>
                    p.OrganizerId == userId ||
                    p.Members.Any(pm => pm.UserId == userId)
                )
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        // GET: /Projects/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Aici iau userul curent ca sa verific daca e organizer / admin
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Aici nu permit edit pentru proiectele inactive (soft deleted)
            if (!project.IsActive)
                return NotFound();

            // Aici permit edit doar daca sunt admin sau organizer
            if (!User.IsInRole("Admin") && project.OrganizerId != userId)
                return Forbid();

            var vm = new ProjectEditVm
            {
                Id = project.Id,
                Title = project.Title,
                Description = project.Description
            };

            return View(vm);
        }

        // POST: /Projects/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProjectEditVm vm)
        {
            if (id != vm.Id)
                return BadRequest();

            // Aici iau userul curent ca sa verific drepturi
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = await _db.Projects
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            if (!project.IsActive)
                return NotFound();

            if (!User.IsInRole("Admin") && project.OrganizerId != userId)
                return Forbid();

            if (!ModelState.IsValid)
                return View(vm);

            // Aici fac trim simplu ca sa evit spatii inutile
            project.Title = vm.Title.Trim();
            project.Description = vm.Description.Trim();

            await _db.SaveChangesAsync();

            TempData["Success"] = "Project updated successfully.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        // GET: /Projects/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // Aici iau id-ul userului logat
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            // Aici iau proiectul din baza de date
            // Incarc si membrii + task-urile ca sa le pot afisa in view
            var project = await _db.Projects
                .AsNoTracking()
                // Aici iau membrii + userul din spatele fiecarui membru, ca sa am UserName/Email
                .Include(p => p.Members)
                    .ThenInclude(pm => pm.User)
                .Include(p => p.TaskItems)
                .Include(p => p.Summaries)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Daca proiectul e soft-deleted, nu trebuie sa poata fi accesat
            if (!project.IsActive)
                return NotFound();

            // Verific daca userul este organizer
            var isOrganizer = project.OrganizerId == userId;

            // Verific daca userul este membru in proiect
            var isMember = project.Members.Any(m => m.UserId == userId);

            // Daca nu e organizer, nu e membru si nu e admin => acces interzis
            if (!isOrganizer && !isMember && !User.IsInRole("Admin"))
                return Forbid();

            // Aici iau userul organizer ca sa pot afisa UserName/Email, nu doar OrganizerId (GUID)
            var organizerUser = await _userManager.FindByIdAsync(project.OrganizerId);

            // Aici aleg ce afisez: username, daca nu exista atunci email, daca nici ala nu exista raman pe id
            ViewBag.OrganizerName = organizerUser?.UserName ?? organizerUser?.Email ?? project.OrganizerId;

            return View(project);
        }

        // GET: /Projects/Members/5
        [HttpGet]
        public async Task<IActionResult> Members(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Aici nu mai permit acces la pagina de membri pentru proiectele sterse/inactive
            if (!project.IsActive)
                return NotFound();

            // Organizatorul vede sigur; membrii vor vedea la Task 5 (validari roluri)
            var isOrganizer = project.OrganizerId == userId;

            // lista membri (include user)
            var members = await _db.ProjectMembers
                .Where(pm => pm.ProjectId == id)
                .Select(pm => new MemberItemVm
                {
                    UserId = pm.UserId,
                    FullName = pm.User.FullName,
                    UserName = pm.User.UserName!,
                    Email = pm.User.Email!
                })
                .OrderBy(m => m.FullName)
                .ToListAsync();

            var vm = new ProjectMembersVm
            {
                ProjectId = project.Id,
                ProjectTitle = project.Title,
                IsOrganizer = isOrganizer,
                OrganizerId = project.OrganizerId,
                Members = members
            };

            return View(vm);
        }

        // POST: /Projects/AddMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(ProjectMembersVm vm)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == vm.ProjectId);
            if (project == null)
                return NotFound();

            // Aici nu permit modificari pe un proiect sters/inactiv
            if (!project.IsActive)
                return NotFound();

            // doar organizatorul poate gestiona membri
            if (project.OrganizerId != userId)
                return Forbid();

            if (!ModelState.IsValid)
                return await Members(vm.ProjectId);

            var input = vm.UserIdentifier.Trim();

            // cautam utilizator dupa username SAU email
            var user = await _userManager.FindByNameAsync(input)
                       ?? await _userManager.FindByEmailAsync(input);

            if (user == null)
            {
                TempData["Error"] = "User not found (check email/username).";
                return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
            }

            // nu adaugam organizerul de 2 ori / nici duplicate
            var exists = await _db.ProjectMembers.AnyAsync(pm => pm.ProjectId == vm.ProjectId && pm.UserId == user.Id);
            if (exists)
            {
                TempData["Error"] = "User is already a member of this project.";
                return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
            }

            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = vm.ProjectId,
                UserId = user.Id
            });

            await _db.SaveChangesAsync();

            TempData["Success"] = "Member added successfully.";
            return RedirectToAction(nameof(Members), new { id = vm.ProjectId });
        }

        // POST: /Projects/RemoveMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int projectId, string memberId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                return NotFound();

            // Aici nu permit modificari pe un proiect sters/inactiv
            if (!project.IsActive)
                return NotFound();

            if (project.OrganizerId != userId)
                return Forbid();

            // nu permitem sa "scoata" organizatorul (ca sa nu ramana proiect fara owner)
            if (memberId == project.OrganizerId)
            {
                TempData["Error"] = "You cannot remove the organizer from the project.";
                return RedirectToAction(nameof(Members), new { id = projectId });
            }

            var membership = await _db.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == memberId);

            if (membership == null)
                return RedirectToAction(nameof(Members), new { id = projectId });

            _db.ProjectMembers.Remove(membership);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Member removed successfully.";
            return RedirectToAction(nameof(Members), new { id = projectId });
        }

        // GET: /Projects/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            Console.WriteLine($">>> ProjectsController.Delete GET called with id={id}");

            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            Console.WriteLine($">>> Project found: {(project != null ? project.Title : "NULL")}");

            if (project == null)
                return NotFound();

            if (!project.IsActive)
                return NotFound();

            if (!await IsAdminOrOrganizerAsync(project))
                return Forbid();

            return View(project);
        }

        // POST: /Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                return NotFound();

            if (!project.IsActive)
                return RedirectToAction(nameof(My));

            if (!await IsAdminOrOrganizerAsync(project))
                return Forbid();

            project.IsActive = false;

            await _db.SaveChangesAsync();

            TempData["ProjectDeleteSuccess"] = "Project deleted successfully.";
            return RedirectToAction(nameof(My));
        }

        private async Task<bool> IsAdminOrOrganizerAsync(Project project)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return User.IsInRole("Admin") || project.OrganizerId == userId;
        }

        //AI SUMMARY
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> GenerateSummary(int projectId)
        {
            // Aici iau userul curent; daca nu e logat, nu are cum sa genereze
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Aici iau proiectul cu membrii ca sa pot verifica drepturile (member/organizer/admin)
            var project = await _db.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null || !project.IsActive)
                return NotFound();

            // Aici verific drepturile: Admin / Organizer / membru activ
            var isAdmin = User.IsInRole("Admin");
            var isOrganizer = project.OrganizerId == userId;
            var isActiveMember = project.Members.Any(m => m.UserId == userId && m.IsActive);

            if (!isAdmin && !isOrganizer && !isActiveMember)
                return Forbid();

            // aici se apeleaza AI-ul (Gemini)
            var aiResult = await _projectSummaryAiService.GenerateProjectSummaryAsync(projectId);

            // daca AI-ul nu raspunde, salvam un mesaj safe
            // Pun si protectie la null, ca sa nu dea warning / exceptie pe Content
            var contentToSave = aiResult.Success && !string.IsNullOrWhiteSpace(aiResult.Summary)
                ? aiResult.Summary
                : "AI summary could not be generated at this time.";

            // salvam rezultatul in baza de date
            var summary = new ProjectSummary
            {
                ProjectId = projectId,
                Content = contentToSave,
                GeneratedAt = DateTime.UtcNow
            };

            _db.ProjectSummaries.Add(summary);
            await _db.SaveChangesAsync();

            // Aici trimit inapoi la Details direct pe sectiunea de AI summary
            var url = Url.Action("Details", "Projects", new { id = projectId }) ?? "/Projects/My";
            return Redirect(url + "#ai-summary");

        }

    }
}
