using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.Projects;

namespace TaskManager.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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

            // acum că avem My(), redirect acolo:
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

        // GET: /Projects/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            // Aici iau userId ca sa stiu daca userul are voie sa vada proiectul (organizer sau membru)
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Challenge();

            // Aici iau proiectul + verific ca userul e organizer sau membru, altfel nu are ce cauta aici
            var project = await _db.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
                return NotFound();

            // Aici ma asigur ca un proiect "sters" (IsActive=false) nu mai poate fi accesat nici din URL
            if (!project.IsActive)
                return NotFound();

            var isOrganizer = project.OrganizerId == userId;
            var isMember = project.Members.Any(pm => pm.UserId == userId);

            // Aici blochez accesul daca userul nu e nici organizer, nici membru, nici admin
            if (!isOrganizer && !isMember && !User.IsInRole("Admin"))
                return Forbid();

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
                OrganizerId = project.OrganizerId,   // <-- important
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
                // daca ai Role = "Member", seteaza aici
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
            // Aici pun un log simplu ca sa fiu sigur ca intru in actiunea asta si vad ce id vine din URL
            Console.WriteLine($">>> ProjectsController.Delete GET called with id={id}");

            // Aici caut proiectul dupa id; daca nu exista, e normal sa dau NotFound
            var project = await _db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            // Aici mai pun un log ca sa vad daca am gasit proiectul sau nu
            Console.WriteLine($">>> Project found: {(project != null ? project.Title : "NULL")}");

            if (project == null)
                return NotFound();

            // Aici nu mai permit stergere/confirmare pentru proiectele deja inactive
            if (!project.IsActive)
                return NotFound();

            // Aici verific drepturile: Admin sau Organizer
            if (!await IsAdminOrOrganizerAsync(project))
                return Forbid();

            // Aici e esential: trimit proiectul ca Model catre view
            return View(project);
        }

        // POST: /Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Aici iau proiectul real (tracked), ca sa pot modifica IsActive
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                return NotFound();

            // Aici daca e deja inactiv, nu mai fac nimic (doar ma intorc la lista)
            if (!project.IsActive)
                return RedirectToAction(nameof(My));

            // Aici verific iar drepturile, pentru ca POST-ul e actiunea care chiar modifica datele
            if (!await IsAdminOrOrganizerAsync(project))
                return Forbid();

            // Aici fac soft delete ca sa evit probleme cu FK (task-uri, membri, comentarii)
            project.IsActive = false;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Project deleted successfully.";
            return RedirectToAction(nameof(My));
        }

        private async Task<bool> IsAdminOrOrganizerAsync(Project project)
        {
            // Aici iau userId ca sa pot compara daca eu sunt organizerul proiectului
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            // Aici permit stergere daca sunt admin sau daca sunt organizer (creatorul proiectului)
            return User.IsInRole("Admin") || project.OrganizerId == userId;
        }
    }
}
