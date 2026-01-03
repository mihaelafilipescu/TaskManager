// UserManager si SignInManager vin din ASP.NET Identity
// Ele se ocupa de creare user + login
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

// Controller, IActionResult, View etc.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
// Modelele noastre: ApplicationUser si RegisterViewModel
using TaskManager.Models;

namespace TaskManager.Controllers
{
    // Controller pentru actiuni legate de cont (Register, Login, Logout)
    public class AccountController : Controller
    {
        // UserManager = creeaza useri, valideaza parole, salveaza in DB
        private readonly UserManager<ApplicationUser> _userManager;

        // SignInManager = autentifica userul (login)
        private readonly SignInManager<ApplicationUser> _signInManager;

        // Constructorul primeste dependentele prin Dependency Injection
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET /Account/Register
        // Afiseaza pagina cu formularul de register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST /Account/Register
        // Primeste datele din formular
        [HttpPost]
        [ValidateAntiForgeryToken] // Protectie pentru formulare
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            // Verificam daca datele din formular sunt valide
            if (!ModelState.IsValid)
                return View(model);

            // Cream userul nostru custom (ApplicationUser)
            var user = new ApplicationUser
            {
                // Identity cere UserName
                UserName = model.Email,

                // Email-ul utilizatorului
                Email = model.Email,

                // Camp custom (obligatoriu in DB)
                FullName = model.FullName,

                // Data crearii contului
                CreatedAt = DateTime.UtcNow,

                // User activ
                IsActive = true
            };

            // Incercam sa salvam userul in baza de date
            var result = await _userManager.CreateAsync(user, model.Password);

            // Daca apar erori (ex: parola slaba)
            if (!result.Succeeded)
            {
                // Adaugam erorile ca sa fie afisate in View
                foreach (var err in result.Errors)
                    ModelState.AddModelError("", err.Description);

                return View(model);
            }

            // Daca userul a fost creat, il logam automat
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Redirect catre pagina principala
            return RedirectToAction("Index", "Home");
        }

        // POST /Account/Logout
        // Delogheaza userul (sterge cookie-ul)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Stergem autentificarea curenta
            await _signInManager.SignOutAsync();

            // Dupa logout, mergem la Home
            return RedirectToAction("Index", "Home");
        }

        // GET /Account/Logout
        // Varianta simpla ca sa poti testa din browser fara formular
       
        [HttpGet]
        public async Task<IActionResult> LogoutGet()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }




        // GET /Account/Login
        [HttpGet]
        [AllowAnonymous] // imi permit acces fara autentificare
        public IActionResult Login(string? returnUrl = null)
        {
            // imi salvez url-ul unde vreau sa revin dupa login
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            // imi salvez returnUrl ca sa-l pot trimite inapoi in view daca am erori
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // incerc login cu email + parola
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Login invalid.");
                return View(model);
            }

            // daca am returnUrl, ma intorc acolo
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            // altfel ma duc pe Home
            return RedirectToAction("Index", "Home");
        }

    }
}
