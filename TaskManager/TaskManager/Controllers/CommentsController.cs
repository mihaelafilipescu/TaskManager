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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Create(CommentFormViewModel model)
        {
            // Iau id-ul userului logat. Daca nu exista, inseamna ca nu ar trebui sa fie aici.
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Caut task-ul si incarc proiectul + membrii, ca sa pot verifica daca userul are voie sa comenteze.
            var task = await _db.TaskItems
                .Include(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(t => t.Id == model.TaskId);

            if (task == null)
                return NotFound();

            // Verific accesul la proiect: member / organizer / admin.
            if (!CanAccessProject(task.Project, userId))
                return Forbid();

            // Cerinta: textul comentariului sa nu fie gol.
            if (string.IsNullOrWhiteSpace(model.Text))
                ModelState.AddModelError(nameof(model.Text), "Comment text cannot be empty.");

            // Daca nu e valid, nu raman pe pagina de create separata, doar revin in Task Details.
            if (!ModelState.IsValid)
                return RedirectToAction("Details", "TaskItems", new { projectId = task.ProjectId, id = task.Id });

            // Creez efectiv comentariul.
            var entity = new Comment
            {
                TaskId = task.Id,
                UserId = userId,
                Text = model.Text.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null,
                IsDeleted = false
            };

            _db.Comments.Add(entity);
            await _db.SaveChangesAsync();

            // Dupa post, ma intorc in Task Details ca sa vad comentariul in lista.
            return RedirectToAction("Details", "TaskItems", new { projectId = task.ProjectId, id = task.Id });
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Edit(int id)
        {
            // Iau id-ul userului logat.
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Incarc comentariul + task + proiect + membri, ca sa pot verifica accesul.
            var comment = await _db.Comments
                .Include(c => c.TaskItem)
                .ThenInclude(t => t.Project)
                .ThenInclude(p => p.Members)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null || comment.IsDeleted)
                return NotFound();

            // Verific ca userul are acces la proiectul acestui task.
            if (!CanAccessProject(comment.TaskItem.Project, userId))
                return Forbid();

            // Regula: doar autorul poate edita (sau Admin).
            if (comment.UserId != userId && !User.IsInRole("Admin"))
                return Forbid();

            // Pregatesc datele pentru form-ul de edit.
            var vm = new CommentEditViewModel
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                ProjectId = comment.TaskItem.ProjectId,
                Text = comment.Text
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async System.Threading.Tasks.Task<IActionResult> Edit(CommentEditViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Reincarc comentariul din DB, ca sa modific corect entitatea urmarita de EF.
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

            // Cerinta: textul sa nu fie gol.
            if (string.IsNullOrWhiteSpace(model.Text))
                ModelState.AddModelError(nameof(model.Text), "Comment text cannot be empty.");

            if (!ModelState.IsValid)
                return View(model);

            // Actualizez textul + timestamp de update.
            comment.Text = model.Text.Trim();
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "TaskItems", new { projectId = model.ProjectId, id = model.TaskId });
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            // Incarc comentariul + user (ca sa afisez autorul) + proiect (pentru acces).
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

            // Regula: doar autorul sterge (sau Admin).
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

            // Aici fac soft delete: nu sterg randul, doar il “ascund”.
            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return RedirectToAction("Details", "TaskItems", new { projectId = comment.TaskItem.ProjectId, id = comment.TaskId });
        }

        private bool CanAccessProject(Project project, string userId)
        {
            // Daca proiectul e dezactivat (soft delete), nu mai permitem acces pe el.
            if (!project.IsActive)
                return false;

            // Organizer e userul care a creat proiectul.
            var isOrganizer = project.OrganizerId == userId;

            // Member e in tabelul asociativ ProjectMembers.
            var isMember = project.Members.Any(m => m.UserId == userId);

            // Admin are acces by default.
            return isOrganizer || isMember || User.IsInRole("Admin");
        }
    }
}
