using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskStatusEnum = TaskManager.Models.TaskStatus;

namespace TaskManager.Services
{
    public class ProjectSummaryService
    {
        private readonly ApplicationDbContext _db;

        public ProjectSummaryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public string GenerateSummary(int projectId)
        {
            // imi iau toate task-urile proiectului ca sa calculez progresul
            var tasks = _db.TaskItems
                .Where(t => t.ProjectId == projectId)
                .AsNoTracking()
                .ToList();

            // daca nu exista task-uri, nu am din ce sa construiesc rezumatul
            if (!tasks.Any())
                return "There are no recent updates for this project.";

            var total = tasks.Count;
            var completed = tasks.Count(t => t.Status == TaskStatusEnum.Completed);
            var inProgress = tasks.Count(t => t.Status == TaskStatusEnum.InProgress);
            var notStarted = tasks.Count(t => t.Status == TaskStatusEnum.NotStarted);

            // caut cel mai apropiat deadline din viitor
            var nextDeadline = tasks
                .Where(t => t.EndDate >= DateTime.Now)
                .OrderBy(t => t.EndDate)
                .FirstOrDefault();

            var summary = $"This project has {total} tasks: {completed} completed, {inProgress} in progress, {notStarted} not started.";

            if (nextDeadline != null)
            {
                summary += $" Next deadline: \"{nextDeadline.Title}\" on {nextDeadline.EndDate:dd MMM yyyy}.";
            }

            return summary;
        }
    }
}
