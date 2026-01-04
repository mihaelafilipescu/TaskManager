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
                .Where(p =>
                    p.OrganizerId == userId ||
                    p.Members.Any(pm => pm.UserId == userId)
                )
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
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
    }
}
