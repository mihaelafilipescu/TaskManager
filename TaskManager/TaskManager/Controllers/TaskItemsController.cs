using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        // PAGINA DE DETALII PENTRU UN TASK (AICI APAR COMENTARIILE + ASIGNARE + STATUS)
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

            // Asignarea "curenta" = ultimul rand din TaskAssignments (istoric)
            var currentAssignment = await _db.TaskAssignments
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.TaskId == id)
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefaultAsync();

            // Organizer/Admin pot asigna si pot schimba status oricand
            var canAssign = await CanModifyTasks(projectId);

            // Membrul poate schimba status doar daca e asignat curent
            var isCurrentAssignee = currentAssignment != null && currentAssignment.UserId == currentUserId;
            var canChangeStatus = canAssign || isCurrentAssignee;

            // Pentru dropdown-ul de assign: iau doar oamenii din proiect (membri + organizator)
            var members = await _db.ProjectMembers
                .AsNoTracking()
                .Include(pm => pm.User)
                .Where(pm => pm.ProjectId == projectId && pm.IsActive)
                .Select(pm => pm.User)
                .ToListAsync();

            var organizer = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Organizer)
                .Where(p => p.Id == projectId)
                .Select(p => p.Organizer)
                .FirstOrDefaultAsync();

            var memberOptions = new List<SelectListItem>();

            // Aici NU mai pun "Unassigned". Doar useri reali din proiect.
            if (organizer != null)
            {
                memberOptions.Add(new SelectListItem
                {
                    Value = organizer.Id,
                    Text = $"{organizer.FullName} ({organizer.UserName})",
                    Selected = currentAssignment?.UserId == organizer.Id
                });
            }

            foreach (var m in members)
            {
                // Evit duplicat daca organizatorul apare si ca membru
                if (organizer != null && m.Id == organizer.Id)
                    continue;

                memberOptions.Add(new SelectListItem
                {
                    Value = m.Id,
                    Text = $"{m.FullName} ({m.UserName})",
                    Selected = currentAssignment?.UserId == m.Id
                });
            }

            var vm = new TaskDetailsViewModel
            {
                Task = task,
                Comments = comments,
                CurrentUserId = currentUserId,

                CurrentAssigneeId = currentAssignment?.UserId,
                CurrentAssigneeLabel = currentAssignment?.User == null
                    ? null
                    : $"{currentAssignment.User.FullName} ({currentAssignment.User.UserName})",

                CanAssign = canAssign,
                CanChangeStatus = canChangeStatus,

                AssignForm = new TaskAssignViewModel
                {
                    TaskId = id,
                    ProjectId = projectId,
                    SelectedUserId = currentAssignment?.UserId ?? string.Empty,
                    MemberOptions = memberOptions
                },

                StatusForm = new TaskStatusUpdateViewModel
                {
                    TaskId = id,
                    ProjectId = projectId,
                    NewStatus = task.Status
                },

                NewComment = new CommentFormViewModel
                {
                    TaskId = id,
                    Text = string.Empty
                }
            };

            return View(vm);
        }

        // LISTA TASK-URI ASIGNATE USERULUI CURENT
        // GET: /TaskItems/MyAssigned
        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> MyAssigned()
        {
            // Iau userul logat
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // IMPORTANT:
            // EF uneori crapa pe GroupBy + FirstOrDefault (EmptyProjectionMember).
            // Ca sa evit, iau assignarile in memorie si calculez "asignarea curenta" per task.
            // La laborator e ok, pentru ca numarul de randuri e mic.
            var allAssignments = await _db.TaskAssignments
                .AsNoTracking()
                .OrderByDescending(a => a.AssignedAt)
                .ToListAsync();

            // Aici imi fac "ultimul assignment" pentru fiecare TaskId:
            // fiind sortate desc dupa AssignedAt, primul pe care il vad pentru un task e cel curent
            var latestByTask = allAssignments
                .GroupBy(a => a.TaskId)
                .Select(g => g.First())
                .ToList();

            // Filtrez doar task-urile unde asignarea curenta e catre userul logat
            var taskIdsAssignedToMeNow = latestByTask
                .Where(a => a.UserId == userId)
                .Select(a => a.TaskId)
                .Distinct()
                .ToList();

            if (taskIdsAssignedToMeNow.Count == 0)
                return View(new List<TaskItem>());

            // Iau task-urile efective + proiectul, ca sa afisez frumos in tabel
            var tasks = await _db.TaskItems
                .AsNoTracking()
                .Include(t => t.Project)
                .Where(t => taskIdsAssignedToMeNow.Contains(t.Id))
                .OrderBy(t => t.EndDate)
                .ToListAsync();

            return View(tasks);
        }

        // ASIGNARE TASK (ORGANIZER / ADMIN)
        // POST: /TaskItems/Assign
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Assign([Bind(Prefix = "AssignForm")] TaskAssignViewModel model)
        {
            if (!await CanModifyTasks(model.ProjectId))
                return Forbid();

            var currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(currentUserId))
                return Forbid();

            // Verific ca task-ul exista in proiect
            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.Id == model.TaskId && t.ProjectId == model.ProjectId);

            if (task == null)
                return NotFound();

            // Daca nu a selectat nimic (sau a venit gol), nu fac nimic
            if (string.IsNullOrWhiteSpace(model.SelectedUserId))
            {
                TempData["AssignError"] = "Please select a valid member.";
                return RedirectToAction("Details", new { projectId = model.ProjectId, id = model.TaskId });
            }

            // Verific ca userul selectat chiar apartine proiectului (member sau organizer)
            var project = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == model.ProjectId);

            if (project == null || !project.IsActive)
                return NotFound();

            var isOrganizer = project.OrganizerId == model.SelectedUserId;
            var isMember = project.Members.Any(m => m.UserId == model.SelectedUserId && m.IsActive);

            if (!isOrganizer && !isMember)
            {
                TempData["AssignError"] = "You can only assign tasks to project members.";
                return RedirectToAction("Details", new { projectId = model.ProjectId, id = model.TaskId });
            }

            // Pastrez istoric: adaug un rand nou in TaskAssignments
            var assignment = new TaskAssignment
            {
                TaskId = model.TaskId,
                UserId = model.SelectedUserId,
                AssignedById = currentUserId,
                AssignedAt = DateTime.UtcNow
            };

            _db.TaskAssignments.Add(assignment);
            await _db.SaveChangesAsync();

            return RedirectToAction("Details", new { projectId = model.ProjectId, id = model.TaskId });
        }

        // SCHIMBARE STATUS (ORGANIZER/ADMIN sau MEMBRU ASIGNAT)
        // POST: /TaskItems/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> UpdateStatus([Bind(Prefix = "StatusForm")] TaskStatusUpdateViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var task = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.Id == model.TaskId && t.ProjectId == model.ProjectId);

            if (task == null)
                return NotFound();

            var canModify = await CanModifyTasks(model.ProjectId);

            if (!canModify)
            {
                // Daca nu e organizer/admin, verific ca e asignat curent
                var currentAssigneeId = await _db.TaskAssignments
                    .AsNoTracking()
                    .Where(a => a.TaskId == model.TaskId)
                    .OrderByDescending(a => a.AssignedAt)
                    .Select(a => a.UserId)
                    .FirstOrDefaultAsync();

                if (currentAssigneeId != userId)
                    return Forbid();
            }

            task.Status = model.NewStatus;
            await _db.SaveChangesAsync();

            return RedirectToAction("Details", new { projectId = model.ProjectId, id = model.TaskId });
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
