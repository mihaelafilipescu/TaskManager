using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.Comments;

namespace TaskManager.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommentsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // POST: /Comments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Create([Bind(Prefix = "NewComment")] CommentFormViewModel model)
        {
            // Aici iau userul logat
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Scot spatiile ca sa nu-mi treaca "   " drept comentariu
            model.Text = (model.Text ?? string.Empty).Trim();

            // Cerinta: textul sa nu fie gol
            if (string.IsNullOrWhiteSpace(model.Text))
                ModelState.AddModelError(nameof(model.Text), "Comment text cannot be empty.");

            // Aici iau task-ul + proiect + membri, ca sa verific accesul
            var task = await _db.TaskItems
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == model.TaskId);

            if (task == null)
                return NotFound();

            if (!CanAccessProject(task.Project, userId))
                return Forbid();

            if (!ModelState.IsValid)
            {
                // Nu ma duc pe pagina /Comments/Create, ma intorc in Details si afisez eroarea
                TempData["CommentError"] = "Comment text cannot be empty.";
                return RedirectToAction("Details", "TaskItems", new { projectId = task.ProjectId, id = task.Id });
            }

            var entity = new Comment
            {
                TaskId = task.Id,
                UserId = userId,
                Text = model.Text,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                IsDeleted = false
            };

            _db.Comments.Add(entity);
            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "TaskItems", new { projectId = task.ProjectId, id = task.Id });
        }

        // GET: /Comments/Edit?id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var comment = await _db.Comments
                .Include(c => c.TaskItem)
                .ThenInclude(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null || comment.IsDeleted)
                return NotFound();

            if (!CanAccessProject(comment.TaskItem.Project, userId))
                return Forbid();

            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            var vm = new CommentEditViewModel
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                ProjectId = comment.TaskItem.ProjectId,
                Text = comment.Text
            };

            return View(vm);
        }

        // POST: /Comments/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Edit(CommentEditViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            model.Text = (model.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(model.Text))
                ModelState.AddModelError(nameof(model.Text), "Comment text cannot be empty.");

            var comment = await _db.Comments
                .Include(c => c.TaskItem)
                .ThenInclude(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(c => c.Id == model.Id);

            if (comment == null || comment.IsDeleted)
                return NotFound();

            if (!CanAccessProject(comment.TaskItem.Project, userId))
                return Forbid();

            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            comment.Text = model.Text;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "TaskItems", new { projectId = model.ProjectId, id = model.TaskId });
        }

        // GET: /Comments/Delete?id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var comment = await _db.Comments
                .Include(c => c.TaskItem)
                .ThenInclude(t => t.Project)
                .ThenInclude(p => p.Members)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null || comment.IsDeleted)
                return NotFound();

            if (!CanAccessProject(comment.TaskItem.Project, userId))
                return Forbid();

            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            var vm = new CommentDeleteViewModel
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                ProjectId = comment.TaskItem.ProjectId,
                Text = comment.Text,
                AuthorEmail = comment.User?.Email ?? "Unknown"
            };

            return View(vm);
        }

        // POST: /Comments/DeleteConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var comment = await _db.Comments
                .Include(c => c.TaskItem)
                .ThenInclude(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null || comment.IsDeleted)
                return NotFound();

            if (!CanAccessProject(comment.TaskItem.Project, userId))
                return Forbid();

            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "TaskItems", new { projectId = comment.TaskItem.ProjectId, id = comment.TaskId });
        }

        private bool CanAccessProject(Project project, string userId)
        {
            if (!project.IsActive)
                return false;

            var isOrganizer = project.OrganizerId == userId;
            var isMember = project.Members.Any(m => m.UserId == userId);

            return isOrganizer || isMember || User.IsInRole("Admin");
        }
    }
}
