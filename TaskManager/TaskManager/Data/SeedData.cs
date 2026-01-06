using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TaskManager.Models;

namespace TaskManager.Data
{
    public static class SeedData
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            // Imi creez scope ca sa pot folosi serviciile corect (DbContext, UserManager, RoleManager)
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Aplic migrarile automat, ca sa nu uit sa updatez DB-ul
            await db.Database.MigrateAsync();

            // Rolul de admin il tin consistent cu restul aplicatiei (eu folosesc "Admin")
            const string adminRole = "Admin";
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(adminRole));
                if (!roleResult.Succeeded)
                {
                    var msg = string.Join("; ", roleResult.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new Exception($"Seed role failed: {msg}");
                }
            }

            // Creez minim 3 useri: 1 admin + 2 membri
            var admin = await EnsureUserAsync(
                userManager,
                email: "admin@local",
                password: "Admin123!",
                fullName: "Administrator",
                makeAdmin: true,
                adminRole: adminRole
            );

            var user1 = await EnsureUserAsync(
                userManager,
                email: "ana@local",
                password: "Member123!",
                fullName: "Ana Member",
                makeAdmin: false,
                adminRole: adminRole
            );

            var user2 = await EnsureUserAsync(
                userManager,
                email: "bob@local",
                password: "Member123!",
                fullName: "Bob Member",
                makeAdmin: false,
                adminRole: adminRole
            );

            // Daca deja am minimul cerut (3 proiecte si 5 task-uri), nu mai inserez iar
            if (await db.Projects.CountAsync() >= 3 && await db.TaskItems.CountAsync() >= 5)
                return;

            // Creez 3 proiecte, fiecare cu un organizator (creatorul proiectului)
            var p1 = await EnsureProjectAsync(db, "Website Redesign", "UI refresh + landing page improvements.", admin.Id);
            var p2 = await EnsureProjectAsync(db, "Sprint Planning", "Backlog + assignments + status rules.", user1.Id);
            var p3 = await EnsureProjectAsync(db, "AI Summary", "Project summary generation + save in DB.", user2.Id);

            // Adaug membri in proiecte (ca sa am echipe reale, nu proiecte goale)
            await EnsureProjectMemberAsync(db, p1.Id, admin.Id);
            await EnsureProjectMemberAsync(db, p1.Id, user1.Id);

            await EnsureProjectMemberAsync(db, p2.Id, user1.Id);
            await EnsureProjectMemberAsync(db, p2.Id, user2.Id);

            await EnsureProjectMemberAsync(db, p3.Id, user2.Id);
            await EnsureProjectMemberAsync(db, p3.Id, admin.Id);

            // Creez minim 5 task-uri cu date valide (EndDate > StartDate)
            // Folosesc explicit TaskManager.Models.TaskStatus ca sa evit conflictul cu System.Threading.Tasks.TaskStatus
            await EnsureTaskAsync(db,
                projectId: p1.Id,
                createdById: admin.Id,
                title: "Create landing hero",
                description: "Add hero section with CTA and clean layout.",
                status: TaskManager.Models.TaskStatus.NotStarted,
                start: DateTime.UtcNow.Date.AddDays(1),
                end: DateTime.UtcNow.Date.AddDays(7),
                mediaType: MediaType.Text,
                mediaContent: "Hero copy + CTA text."
            );

            await EnsureTaskAsync(db,
                projectId: p1.Id,
                createdById: admin.Id,
                title: "Add testimonials section",
                description: "Add 3 testimonials and make it responsive.",
                status: TaskManager.Models.TaskStatus.InProgress,
                start: DateTime.UtcNow.Date.AddDays(1),
                end: DateTime.UtcNow.Date.AddDays(5),
                mediaType: MediaType.Text,
                mediaContent: "Dummy testimonials content."
            );

            await EnsureTaskAsync(db,
                projectId: p2.Id,
                createdById: user1.Id,
                title: "Implement task assignment UI",
                description: "UI to assign tasks to project members.",
                status: TaskManager.Models.TaskStatus.NotStarted,
                start: DateTime.UtcNow.Date.AddDays(2),
                end: DateTime.UtcNow.Date.AddDays(9),
                mediaType: MediaType.Text,
                mediaContent: "Dropdown for members + save action."
            );

            await EnsureTaskAsync(db,
                projectId: p2.Id,
                createdById: user1.Id,
                title: "Permissions + status flow",
                description: "Member can change status only if assigned.",
                status: TaskManager.Models.TaskStatus.NotStarted,
                start: DateTime.UtcNow.Date.AddDays(2),
                end: DateTime.UtcNow.Date.AddDays(10),
                mediaType: MediaType.Text,
                mediaContent: "Rules + validation notes."
            );

            await EnsureTaskAsync(db,
                projectId: p3.Id,
                createdById: user2.Id,
                title: "Store AI summary in DB",
                description: "Save AI response into ProjectSummaries and display it.",
                status: TaskManager.Models.TaskStatus.Completed,
                start: DateTime.UtcNow.Date.AddDays(-10),
                end: DateTime.UtcNow.Date.AddDays(-2),
                mediaType: MediaType.Text,
                mediaContent: "Placeholder summary content."
            );
        }

        private static async Task<ApplicationUser> EnsureUserAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string fullName,
            bool makeAdmin,
            string adminRole)
        {
            // Caut userul dupa email; daca exista, nu il recreez
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                // Construiesc userul cu campurile mele custom
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                // Incerc sa creez userul; daca esueaza, arunc exceptie ca sa vad clar de ce
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new Exception($"Seed user failed for {email}: {msg}");
                }
            }

            // Daca userul trebuie sa fie admin, ma asigur ca are rolul Admin
            if (makeAdmin && !await userManager.IsInRoleAsync(user, adminRole))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, adminRole);
                if (!addRoleResult.Succeeded)
                {
                    var msg = string.Join("; ", addRoleResult.Errors.Select(e => $"{e.Code}:{e.Description}"));
                    throw new Exception($"Add role failed for {email}: {msg}");
                }
            }

            return user;
        }

        private static async Task<Project> EnsureProjectAsync(
            ApplicationDbContext db,
            string title,
            string description,
            string organizerId)
        {
            // Evit dublurile: daca exista proiect cu acelasi Title, il refolosesc
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.Title == title);
            if (existing != null) return existing;

            // Creez proiectul; organizatorul este userul care l-a creat
            var project = new Project
            {
                Title = title,
                Description = description,
                OrganizerId = organizerId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return project;
        }

        private static async Task EnsureProjectMemberAsync(ApplicationDbContext db, int projectId, string userId)
        {
            // Verific daca userul e deja membru in proiect ca sa nu dublez
            var exists = await db.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
            if (exists) return;

            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });

            await db.SaveChangesAsync();
        }

        private static async Task EnsureTaskAsync(
            ApplicationDbContext db,
            int projectId,
            string createdById,
            string title,
            string description,
            TaskManager.Models.TaskStatus status,
            DateTime start,
            DateTime end,
            MediaType mediaType,
            string mediaContent)
        {
            // Evit dublurile: acelasi titlu in acelasi proiect inseamna ca e acelasi task pentru seed
            var exists = await db.TaskItems.AnyAsync(t => t.ProjectId == projectId && t.Title == title);
            if (exists) return;

            db.TaskItems.Add(new TaskItem
            {
                ProjectId = projectId,
                Title = title,
                Description = description,
                Status = status,
                StartDate = start,
                EndDate = end,
                MediaType = mediaType,
                MediaContent = mediaContent,
                CreatedAt = DateTime.UtcNow,
                CreatedById = createdById
            });

            await db.SaveChangesAsync();
        }
    }
}
