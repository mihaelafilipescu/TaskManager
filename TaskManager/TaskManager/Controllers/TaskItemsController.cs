using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Controllers
{
    [Authorize]
    [Route("Projects/{projectId:int}/TaskItems")]
    public class TaskItemsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskItemsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Projects/{projectId}/TaskItems
        [HttpGet("")]
        // folosim System.Threading.Tasks.Task pentru a evita ambiguitatea cu Taskurile noastre
        public async System.Threading.Tasks.Task<IActionResult> Index(int projectId)
        {
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return NotFound();

            var tasks = await _db.TaskItems
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.Project = project;
            return View(tasks);
        }

        // GET: /Projects/{projectId}/TaskItems/Create
        [HttpGet("Create")]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            var today = DateTime.Today;

            var model = new TaskItem
            {
                ProjectId = projectId,
                StartDate = today,
                EndDate = today.AddDays(1)
            };

            return View(model);
        }

        // POST: /Projects/{projectId}/TaskItems/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId, TaskItem model)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            model.ProjectId = projectId;
            model.CreatedById = _userManager.GetUserId(User)!;

            // ✅ normalizează la date calendaristice (fără oră)
            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            // ✅ regula logică
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "Data de finalizare trebuie să fie după data de început.");

            if (!ModelState.IsValid)
                return View(model);

            _db.TaskItems.Add(model);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // GET: /Projects/{projectId}/TaskItems/Edit/5
        [HttpGet("Edit/{id:int}")]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null) return NotFound();

            return View(task);
        }

        // POST: /Projects/{projectId}/TaskItems/Edit/5
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id, TaskItem model)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            if (id != model.Id) return BadRequest();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null) return NotFound();

            // ✅ normalizează la date calendaristice (fără oră)
            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            // ✅ regula logică
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "Data de finalizare trebuie să fie după data de început.");

            if (!ModelState.IsValid)
                return View(model);

            task.Title = model.Title;
            task.Description = model.Description;
            task.Status = model.Status;

            // ✅ salvează tot fără oră
            task.StartDate = model.StartDate;
            task.EndDate = model.EndDate;

            task.MediaType = model.MediaType;
            task.MediaContent = model.MediaContent;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { projectId });
        }

        // GET: /Projects/{projectId}/TaskItems/Delete/5
        [HttpGet("Delete/{id:int}")]
        public async System.Threading.Tasks.Task<IActionResult> Delete(int projectId, int id)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null) return NotFound();

            return View(task);
        }

        // POST: /Projects/{projectId}/TaskItems/Delete/5
        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DeleteConfirmed(int projectId, int id)
        {
            if (!await CanManageTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null) return NotFound();

            _db.TaskItems.Remove(task);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { projectId });
        }

        private async System.Threading.Tasks.Task<bool> CanManageTasks(int projectId)
        {
            if (User.IsInRole("Administrator"))
                return true;

            var userId = _userManager.GetUserId(User);
            if (userId == null)
                return false;

            return await _db.Projects
                .AsNoTracking()
                .AnyAsync(p => p.Id == projectId && p.OrganizerId == userId);
        }
    }
}
