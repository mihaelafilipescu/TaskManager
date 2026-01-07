using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.ViewModels.Home;

namespace TaskManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var model = new HomeIndexViewModel();

            // daca nu e logat, homepage ramane practic ca pana acum
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return View(model);

            // imi iau user-ul curent ca sa pot filtra proiectele
            var userId = _userManager.GetUserId(User);

            // imi iau proiectele in care e organizer sau membru activ
            var projectsQuery = _db.Projects
                .AsNoTracking()
                .Include(p => p.Members)
                .Where(p =>
                    p.IsActive &&
                    (p.OrganizerId == userId ||
                     p.Members.Any(m => m.UserId == userId && m.IsActive)));

            // lista pentru dropdown / links
            model.Projects = await projectsQuery
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectNavItem
                {
                    Id = p.Id,
                    Title = p.Title
                })
                .ToListAsync();

            // ultimul proiect (ca sa am un buton mare "Go to AI Summary")
            model.LatestProjectId = model.Projects.FirstOrDefault()?.Id;

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
