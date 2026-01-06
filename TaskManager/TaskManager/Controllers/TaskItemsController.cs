using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.TaskItems;

namespace TaskManager.Controllers
{
    [Authorize]
    public class TaskItemsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public TaskItemsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        // GET: /TaskItems?projectId=1
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Index(int projectId)
        {
            // Aici verific doar dreptul de VIEW (member are voie sa vada)
            if (!await CanViewTasks(projectId))
                return Forbid();

            // Aici iau proiectul ca sa-l afisez sus in pagina
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return NotFound();

            ViewBag.Project = project;

            // Aici iau task-urile din proiect
            var tasks = await _db.TaskItems
                .AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            // Aici trimit si un flag in view ca sa stie daca afiseaza butoanele (Create/Edit/Delete)
            ViewBag.CanModify = await CanModifyTasks(projectId);

            return View(tasks);
        }

        // GET: /TaskItems/Create?projectId=1
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
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

        // POST: /TaskItems/Create?projectId=1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Create(int projectId, TaskItemFormViewModel model)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
            if (!await CanModifyTasks(projectId))
                return Forbid();

            model.ProjectId = projectId;

            // Aici normalizez datele fara ora
            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            // Aici aplic regula logica (end > start) - mesaj in English
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");

            // Aici validez si salvez media (image/video/text)
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

        // GET: /TaskItems/Edit?projectId=1&id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
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

                // Aici, daca e imagine, nu bag in MediaContent (ca sa nu se bata cu Text/Video),
                // tin imaginea in ExistingImagePath si o afisez separat
                MediaContent = task.MediaType == MediaType.Image ? null : task.MediaContent,
                ExistingImagePath = task.MediaType == MediaType.Image ? task.MediaContent : null
            };

            return View(model);
        }

        // POST: /TaskItems/Edit?projectId=1&id=5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int projectId, int id, TaskItemFormViewModel model)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
            if (!await CanModifyTasks(projectId))
                return Forbid();

            if (id != model.Id)
                return BadRequest();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            // Aici normalizez datele fara ora
            model.StartDate = model.StartDate.Date;
            model.EndDate = model.EndDate.Date;

            // Aici aplic regula logica - mesaj in English
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");

            // Aici validez + tratez media (la edit pot pastra imaginea veche)
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

        // GET: /TaskItems/Delete?projectId=1&id=5
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Delete(int projectId, int id)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
            if (!await CanModifyTasks(projectId))
                return Forbid();

            var task = await _db.TaskItems
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Id == id);

            if (task == null)
                return NotFound();

            return View(task);
        }

        // POST: /TaskItems/Delete?projectId=1&id=5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> DeleteConfirmed(int projectId, int id)
        {
            // Aici verific dreptul de MODIFY (member nu are voie)
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

        private async System.Threading.Tasks.Task<bool> CanViewTasks(int projectId)
        {
            // Aici iau userul logat
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            // Aici iau proiectul cu membrii, ca sa verific rolul userului in proiect
            var project = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return false;

            // Daca proiectul e soft-deleted, nu mai are sens sa-l accesez
            if (!project.IsActive)
                return false;

            var isOrganizer = project.OrganizerId == userId;
            var isMember = project.Members.Any(m => m.UserId == userId);

            // VIEW: organizer / member / admin
            return isOrganizer || isMember || User.IsInRole("Admin");
        }

        private async System.Threading.Tasks.Task<bool> CanModifyTasks(int projectId)
        {
            // Aici iau userul logat
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            // Aici iau proiectul (nu am nevoie de Members pentru modify, imi trebuie doar organizerId)
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return false;

            // Daca proiectul e soft-deleted, nu permit modificari
            if (!project.IsActive)
                return false;

            var isOrganizer = project.OrganizerId == userId;

            // MODIFY: doar organizer / admin
            return isOrganizer || User.IsInRole("Admin");
        }

        private async System.Threading.Tasks.Task ValidateAndHandleMediaAsync(TaskItemFormViewModel model, bool isEdit)
        {
            // Text
            if (model.MediaType == MediaType.Text)
            {
                if (string.IsNullOrWhiteSpace(model.MediaContent))
                    ModelState.AddModelError(nameof(model.MediaContent), "Please add text content.");

                return;
            }

            // Video
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

            // Image
            var hasExisting = !string.IsNullOrWhiteSpace(model.ExistingImagePath);

            if (model.ImageFile == null)
            {
                // La Create trebuie neaparat fisier, la Edit pot pastra imaginea veche
                if (!isEdit || !hasExisting)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Please upload an image.");
                }
                else
                {
                    model.MediaContent = model.ExistingImagePath;
                }

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
