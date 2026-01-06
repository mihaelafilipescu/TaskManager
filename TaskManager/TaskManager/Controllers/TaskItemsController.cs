using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.TaskItems;
using TaskManager.ViewModels.Comments;

namespace TaskManager.Controllers
{
    [Authorize]
    public class TaskItemsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public TaskItemsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        // LISTA DE TASK-URI DINTR-UN PROIECT
        // GET: /TaskItems?projectId=1
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Index(int projectId)
        {
            // Verific ca userul are voie sa vada task-urile proiectului
            if (!await CanViewTasks(projectId))
                return Forbid();

            // Iau proiectul ca sa-l afisez in view (titlu etc.)
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return NotFound();

            ViewBag.Project = project;

            // Iau toate task-urile din proiect
            var tasks = await _db.TaskItems
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            // Flag pentru UI: daca apar sau nu butoanele de edit/delete
            ViewBag.CanModify = await CanModifyTasks(projectId);

            return View(tasks);
        }

        // PAGINA DE DETALII PENTRU UN TASK (AICI APAR COMENTARIILE)
        // GET: /TaskItems/Details?projectId=1&id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Details(int projectId, int id)
        {
            // Verific dreptul de view
            if (!await CanViewTasks(projectId))
                return Forbid();

            // Iau task-ul
            var task = await _db.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            // Iau comentariile active, ordonate cronologic
            var comments = await _db.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .Where(c => c.TaskId == id && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            // Pastrez userul curent ca sa stiu in view ce butoane afisez
            var currentUserId = _userManager.GetUserId(User) ?? string.Empty;

            var vm = new TaskDetailsViewModel
            {
                Task = task,
                Comments = comments,
                CurrentUserId = currentUserId,
                NewComment = new CommentFormViewModel
                {
                    TaskId = id,
                    Text = string.Empty
                }
            };

            return View(vm);
        }

        // CREATE TASK - FORM
        // GET: /TaskItems/Create?projectId=1
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId)
        {
            // Doar organizer/admin pot crea task-uri
            if (!await CanModifyTasks(projectId))
                return Forbid();

            var today = DateTime.Today;

            var model = new TaskItemFormViewModel
            {
                ProjectId = projectId,
                StartDate = today,
                EndDate = today.AddDays(1),
                MediaType = MediaType.Text
            };

            return View(model);
        }

        // CREATE TASK - SAVE
        // POST: /TaskItems/Create?projectId=1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId, TaskItemFormViewModel model)
        {
            if (!await CanModifyTasks(projectId))
                return Forbid();

            model.ProjectId = projectId;

            // Scot ora din date
            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            // Regula logica
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");

            await ValidateAndHandleMediaAsync(model, isEdit: false);

            if (!ModelState.IsValid)
                return View(model);

            var entity = new TaskItem
            {
                ProjectId = projectId,
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                MediaType = model.MediaType,
                MediaContent = model.MediaContent ?? string.Empty,
                CreatedById = _userManager.GetUserId(User)!,
                CreatedAt = DateTime.UtcNow
            };

            _db.TaskItems.Add(entity);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // EDIT TASK - FORM
        // GET: /TaskItems/Edit?projectId=1&id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id)
        {
            if (!await CanModifyTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            var model = new TaskItemFormViewModel
            {
                Id = task.Id,
                ProjectId = task.ProjectId,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                MediaType = task.MediaType,
                MediaContent = task.MediaType == MediaType.Image ? null : task.MediaContent,
                ExistingImagePath = task.MediaType == MediaType.Image ? task.MediaContent : null
            };

            return View(model);
        }

        // EDIT TASK - SAVE
        // POST: /TaskItems/Edit?projectId=1&id=5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id, TaskItemFormViewModel model)
        {
            if (!await CanModifyTasks(projectId))
                return Forbid();

            if (id != model.Id)
                return BadRequest();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");

            await ValidateAndHandleMediaAsync(model, isEdit: true);

            if (!ModelState.IsValid)
                return View(model);

            task.Title = model.Title;
            task.Description = model.Description;
            task.Status = model.Status;
            task.StartDate = model.StartDate;
            task.EndDate = model.EndDate;
            task.MediaType = model.MediaType;
            task.MediaContent = model.MediaContent ?? string.Empty;

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // DELETE TASK - CONFIRM
        // GET: /TaskItems/Delete?projectId=1&id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Delete(int projectId, int id)
        {
            if (!await CanModifyTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            return View(task);
        }

        // DELETE TASK - SAVE
        // POST: /TaskItems/DeleteConfirmed?projectId=1&id=5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DeleteConfirmed(int projectId, int id)
        {
            if (!await CanModifyTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            _db.TaskItems.Remove(task);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { projectId });
        }

        // VERIFICARE DREPT VIEW (MEMBER / ORGANIZER / ADMIN)
        private async System.Threading.Tasks.Task<bool> CanViewTasks(int projectId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var project = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null || !project.IsActive)
                return false;

            var isOrganizer = project.OrganizerId == userId;
            var isMember = project.Members.Any(m => m.UserId == userId);

            return isOrganizer || isMember || User.IsInRole("Admin");
        }

        // VERIFICARE DREPT MODIFY (ORGANIZER / ADMIN)
        private async System.Threading.Tasks.Task<bool> CanModifyTasks(int projectId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null || !project.IsActive)
                return false;

            return project.OrganizerId == userId || User.IsInRole("Admin");
        }

        // LOGICA PENTRU MEDIA (TEXT / VIDEO / IMAGE)
        private async System.Threading.Tasks.Task ValidateAndHandleMediaAsync(TaskItemFormViewModel model, bool isEdit)
        {
            if (model.MediaType == MediaType.Text)
            {
                if (string.IsNullOrWhiteSpace(model.MediaContent))
                    ModelState.AddModelError(nameof(model.MediaContent), "Please add text content.");

                return;
            }

            if (model.MediaType == MediaType.Video)
            {
                if (string.IsNullOrWhiteSpace(model.MediaContent))
                {
                    ModelState.AddModelError(nameof(model.MediaContent), "Please add a video URL (YouTube/Vimeo).");
                    return;
                }

                if (!Uri.TryCreate(model.MediaContent, UriKind.Absolute, out _))
                {
                    ModelState.AddModelError(nameof(model.MediaContent), "The video URL is not valid.");
                    return;
                }

                return;
            }

            var hasExisting = !string.IsNullOrWhiteSpace(model.ExistingImagePath);

            if (model.ImageFile == null)
            {
                if (!isEdit || !hasExisting)
                    ModelState.AddModelError(nameof(model.ImageFile), "Please upload an image.");
                else
                    model.MediaContent = model.ExistingImagePath;

                return;
            }

            if (!model.ImageFile.ContentType.StartsWith("image/"))
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Only image files are allowed.");
                return;
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);

            var ext = Path.GetExtension(model.ImageFile.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }

            model.MediaContent = $"/uploads/{fileName}";
        }
    }
}
