// UserManager si SignInManager vin din ASP.NET Identity
// Ele se ocupa de creare user + login
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

// Controller, IActionResult, View etc.
using Microsoft.AspNetCore.Mvc;
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

            // Verificăm dacă username-ul este deja folosit
            var existingUserByUsername = await _userManager.FindByNameAsync(model.UserName);
            if (existingUserByUsername != null)
            {
                // Mesaj de eroare legat direct de câmpul UserName
                ModelState.AddModelError(nameof(model.UserName), "This username is already taken.");
                return View(model);
            }

            // Verificăm și email-ul
            var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingUserByEmail != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
                return View(model);
            }

            // Cream userul nostru custom (ApplicationUser)
            var user = new ApplicationUser
            {
                // Identity cere UserName
                UserName = model.UserName,

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var input = model.Login.Trim();

            // 1) Cautam user fie dupa username, fie dupa email
            var user = await _userManager.FindByNameAsync(input);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(input);
            }

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid login attempt.");
                return View(model);
            }

            // 2) Incercam logarea cu username-ul real (cel din DB)
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            ModelState.AddModelError("", "Invalid login attempt.");
            return View(model);
        }

    }
}
