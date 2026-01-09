using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskManager.Controllers
{
    [Authorize(Roles = "Admin")]
    public class TestController : Controller
    {
        public IActionResult Index()
            => Content($"Autentificat ca: {User.Identity?.Name}");
    }
}
//test sa verific ca a mers autentificarea 