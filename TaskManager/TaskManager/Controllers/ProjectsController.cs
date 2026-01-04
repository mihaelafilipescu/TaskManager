using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

            // până facem /Projects/My în Task 3, trimitem user-ul pe Home
            return RedirectToAction("Index", "Home");
        }
    }
}
