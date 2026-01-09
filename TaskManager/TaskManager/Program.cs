using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

var builder = WebApplication.CreateBuilder(args);

// Aici setez conexiunea la baza de date, folosind connection string-ul din appsettings.json
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Aici configurez Identity pentru login/register
// Setez sa nu ceara confirmare pe email, ca sa fie simplu la proiect
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
// Aici activez rolurile (Admin etc.)
.AddRoles<IdentityRole>()
// Aici leg Identity de EF Core si de DbContext-ul meu
.AddEntityFrameworkStores<ApplicationDbContext>();

// Aici activez MVC (Controllers + Views)
// Important: o singura data, altfel e duplicat inutil
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Aici tratez erorile diferit in productie vs development
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Aici fortez HTTPS
app.UseHttpsRedirection();

// Aici permit fisiere statice (css/js/img)
app.UseStaticFiles();

// Aici pornesc routing-ul
app.UseRouting();

// Aici pornesc autentificarea (cine esti)
app.UseAuthentication();

// Aici pornesc autorizarea (ce ai voie sa faci)
app.UseAuthorization();

// Asta e ruta default: daca nu scriu nimic, ma duce la Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Asta e necesar pentru paginile Identity (Login/Register)
app.MapRazorPages();

// Aici rulez seed-ul complet ca sa am date de demo in DB
// Daca seed-ul e scris idempotent, nu imi dubleaza datele la fiecare pornire
await SeedData.SeedAsync(app.Services);

app.Run();
