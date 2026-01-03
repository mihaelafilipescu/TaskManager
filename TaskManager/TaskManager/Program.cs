using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

//rol admin+seed
// Functie async care se ocupa de crearea unui admin default
// Se apeleaza o singura data, la pornirea aplicatiei

async System.Threading.Tasks.Task SeedAdminAsync(IServiceProvider services)
{
    Console.WriteLine(">>> SeedAdminAsync started");

    // Cream un scope nou pentru servicii
    // E necesar ca sa putem folosi UserManager si RoleManager corect
    using var scope = services.CreateScope();

    // Luam RoleManager – el se ocupa cu rolurile (Admin, User etc.)
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    // Luam UserManager – el se ocupa cu userii (creare, cautare, parole)
    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // Datele adminului pe care vrem sa-l avem by default
    const string adminRole = "Admin";
    const string adminEmail = "admin@local";
    const string adminPassword = "Admin123!";

    // 1️Verificam daca rolul Admin exista deja
    // Daca nu exista, il cream
    if (!await roleManager.RoleExistsAsync(adminRole))
        await roleManager.CreateAsync(new IdentityRole(adminRole));

    // 2️Cautam userul admin dupa email
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    // Daca NU exista userul admin, il cream
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,

            FullName = "Administrator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);

        Console.WriteLine($">>> Create admin succeeded: {createResult.Succeeded}");
        foreach (var err in createResult.Errors)
            Console.WriteLine($">>> {err.Code}: {err.Description}");
    }

    // 3️ Ne asiguram ca userul este in rolul Admin
    // Daca nu e, il adaugam
    if (!await userManager.IsInRoleAsync(adminUser, adminRole))
        await userManager.AddToRoleAsync(adminUser, adminRole);

    Console.WriteLine(">>> SeedAdminAsync finished");
}

await SeedAdminAsync(app.Services);

app.Run();