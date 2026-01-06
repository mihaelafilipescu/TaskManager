using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.Dashboard;

// alias ca sa nu se bata cu System.Threading.Tasks.TaskStatus
using TaskItemStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Index(
            int? projectId,
            TaskItemStatus? status,
            bool dueSoonOnly = false,
            int? dueInDays = null)
        {
            // Aici setez default-ul daca userul nu a ales un numar valid
            var effectiveDueDays = dueInDays.HasValue && dueInDays.Value > 0 ? dueInDays.Value : 7;

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            // Proiectele userului (doar pt dropdown/filtru)
            var projects = await _db.Projects
                .AsNoTracking()
                .Where(p => p.IsActive)
                .Where(p => p.OrganizerId == userId || p.Members.Any(m => m.UserId == userId && m.IsActive))
                .Select(p => new DashboardProjectItemVm
                {
                    Id = p.Id,
                    Title = p.Title,
                    IsOrganizer = p.OrganizerId == userId
                })
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            var projectIds = projects.Select(p => p.Id).ToList();

            if (projectIds.Count == 0)
            {
                return View(new DashboardIndexViewModel
                {
                    Projects = projects,
                    Filters = new DashboardFiltersVm
                    {
                        ProjectId = projectId,
                        Status = status,
                        DueSoonOnly = dueSoonOnly,
                        DueInDays = effectiveDueDays
                    }
                });
            }

            // 1) Iau toate asignarile pentru task-urile din proiectele userului, prin join pe TaskItems
            // Nu folosesc a.Task (nu exista in modelul tau)
            var assignmentsInMyProjects = await (
                from a in _db.TaskAssignments.AsNoTracking()
                join t in _db.TaskItems.AsNoTracking() on a.TaskId equals t.Id
                where projectIds.Contains(t.ProjectId)
                select new
                {
                    Assignment = a,
                    TaskProjectId = t.ProjectId
                }
            )
            .OrderByDescending(x => x.Assignment.AssignedAt)
            .ToListAsync();

            // 2) Aleg ultima asignare per task (asignarea curenta)
            var currentAssignmentByTaskId = assignmentsInMyProjects
                .Select(x => x.Assignment)
                .GroupBy(a => a.TaskId)
                .Select(g => g.First())
                .ToDictionary(a => a.TaskId, a => a);

            // 3) Task-urile mele = cele unde asignarea curenta e pe mine
            var myTaskIds = currentAssignmentByTaskId
                .Where(kv => kv.Value.UserId == userId)
                .Select(kv => kv.Key)
                .ToList();

            if (myTaskIds.Count == 0)
            {
                return View(new DashboardIndexViewModel
                {
                    Projects = projects,
                    Filters = new DashboardFiltersVm
                    {
                        ProjectId = projectId,
                        Status = status,
                        DueSoonOnly = dueSoonOnly,
                        DueInDays = effectiveDueDays
                    }
                });
            }

            // 4) Iau DOAR task-urile mele + proiectul lor
            var tasksQuery = _db.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Where(t => myTaskIds.Contains(t.Id));

            if (projectId.HasValue)
                tasksQuery = tasksQuery.Where(t => t.ProjectId == projectId.Value);

            if (status.HasValue)
                tasksQuery = tasksQuery.Where(t => t.Status == status.Value);

            if (dueSoonOnly)
            {
                var today = DateTime.UtcNow.Date;
                var limit = today.AddDays(effectiveDueDays);

                tasksQuery = tasksQuery.Where(t =>
                    t.Status != TaskItemStatus.Completed &&
                    t.EndDate.Date >= today &&
                    t.EndDate.Date <= limit
                );
            }

            var tasksRaw = await tasksQuery
                .OrderBy(t => t.EndDate)
                .ToListAsync();

            // 5) Pentru label de assignee imi trebuie userul din asignarea curenta
            var myAssignments = currentAssignmentByTaskId
                .Where(kv => myTaskIds.Contains(kv.Key))
                .Select(kv => kv.Value)
                .ToList();

            var userIds = myAssignments.Select(a => a.UserId).Distinct().ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var todayForCalc = DateTime.UtcNow.Date;

            var tasks = new List<DashboardTaskItemVm>();

            foreach (var t in tasksRaw)
            {
                currentAssignmentByTaskId.TryGetValue(t.Id, out var a);

                var daysUntilDue = (t.EndDate.Date - todayForCalc).Days;

                string? assigneeLabel = null;

                if (a != null && users.TryGetValue(a.UserId, out var u))
                {
                    // Aici afisez cum ai cerut: FullName + username
                    assigneeLabel = $"{u.FullName} ({u.UserName})";
                }

                tasks.Add(new DashboardTaskItemVm
                {
                    Id = t.Id,
                    ProjectId = t.ProjectId,
                    ProjectTitle = t.Project.Title,
                    Title = t.Title,
                    Status = t.Status,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    DaysUntilDue = daysUntilDue,
                    AssigneeLabel = assigneeLabel,
                    IsAssignedToMe = true
                });
            }

            var tasksByStatus = tasks
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.EndDate).ToList());

            var upcomingLimit = todayForCalc.AddDays(effectiveDueDays);

            var upcomingDeadlines = tasks
                .Where(t => t.Status != TaskItemStatus.Completed)
                .Where(t => t.EndDate.Date >= todayForCalc && t.EndDate.Date <= upcomingLimit)
                .OrderBy(t => t.EndDate)
                .Take(5)
                .ToList();

            return View(new DashboardIndexViewModel
            {
                Projects = projects,
                Tasks = tasks,
                TasksByStatus = tasksByStatus,
                UpcomingDeadlines = upcomingDeadlines,
                Filters = new DashboardFiltersVm
                {
                    ProjectId = projectId,
                    Status = status,
                    DueSoonOnly = dueSoonOnly,
                    DueInDays = effectiveDueDays
                }
            });
        }
    }
}
