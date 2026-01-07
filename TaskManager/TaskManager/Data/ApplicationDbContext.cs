using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using TaskManager.Models;

namespace TaskManager.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tabelele din baza de date
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<TaskItem> TaskItems { get; set; }
        public DbSet<TaskAssignment> TaskAssignments { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<ProjectSummary> ProjectSummaries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // aici adaugam relatiile

            //1. Project-organizer (ApplicationUser)
            builder.Entity<Project>()//aici ii spunem sa configureze entitatea Project din baza de date
                .HasOne(p => p.Organizer)//un proiect are un singur organizator           
                .WithMany(u => u.OrganizedProjects)//un applicationuser poate sa aiba mai multe proiecte create   
                .HasForeignKey(p => p.OrganizerId)//cheia straina se afla in Projects 
                .OnDelete(DeleteBehavior.Restrict); //daca se sterge userul nu se sterge si proiectul

            //2. Project - ProjectMember
            builder.Entity<ProjectMember>()
                .HasOne(pm => pm.Project)//un ProjectMember apartine unui proiect
                .WithMany(p => p.Members)//un proiect are mai multi membri
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade); //daca se sterge proiectul se sterg si membrii asociati

            builder.Entity<ProjectMember>()//aici legam ProjectMember de ApplicationUser
                .HasOne(pm => pm.User)//un ProjectMember apartine unui user
                .WithMany(u => u.ProjectMemberships)//un user poate fi membru in mai multe proiecte
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);//daca se sterge userul se sterge si rolul de ProjectMember

            builder.Entity<ProjectMember>()//prevenim duplicatele - un user poate fi ProjectMember o singura data intr-un proiect
                .HasIndex(pm => new { pm.ProjectId, pm.UserId })
                .IsUnique();

            //3. Project - Task
            builder.Entity<TaskItem>()
                .HasOne(t => t.Project)//un task aparine unui proiect
                .WithMany(p => p.TaskItems)//un proiect are mai multe taskuri
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);//daca stergem proiectul se sterg toate taskurile
            //4. Task - TaskAssignment
            builder.Entity<TaskAssignment>()
                .HasOne(ta => ta.TaskItem)//fiecare TaskAssignment apartine unui Task
                .WithMany(t => t.Assignments)//un task poate fi assigned mai multor persoane
                .HasForeignKey(ta => ta.TaskId)
                .OnDelete(DeleteBehavior.NoAction);//aici il las NoAction ca sa evit multiple cascade paths in SQL Server



            //5. TaskAssignment - ApplicationUser
            builder.Entity<TaskAssignment>()
                .HasOne(ta => ta.User)//fiecare asignare e pentru un user unic
                .WithMany(u => u.TaskAssignments)//un user poate fi asignat la mai multe taskuri
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.Cascade);//daca stergem userul se sterg si asignarile

            builder.Entity<TaskAssignment>()
                .HasOne(ta => ta.AssignedBy)//fiecare task este atribuit de un singur user
                .WithMany()
                .HasForeignKey(ta => ta.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);//daca incercam sa stergem un user dar exista taskuri asignate de el bd ul ne opreste


            //6. Task - Comment
            builder.Entity<Comment>()
                .HasOne(c => c.TaskItem)//un comentariu apartine unui task
                .WithMany(t => t.Comments)//un task poate avea mai multe comentarii
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.NoAction);//aici il las NoAction ca sa evit multiple cascade paths in SQL Server

            //7. Comment - ApplicationUser
            builder.Entity<Comment>()
                .HasOne(c => c.User)//un comentariu are un autor
                .WithMany(u => u.Comments)//un user poate lasa mai multe comentarii
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);//daca stergem un user se sterg cu comentariile pe care le-a lasat

            //8. Project - ProjectSummary
            builder.Entity<ProjectSummary>()
                .HasOne(ps => ps.Project)//un rezumat e facut pt un proiect
                .WithMany(p => p.Summaries)//un proiect poate avea mai multe rezumate(ex: facute de persoane diferite)
                .HasForeignKey(ps => ps.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);//daca se sterge un proiect se sterg si rezumatele lui

            ////9. ProjectSummary - ApplicationUser
            //builder.Entity<ProjectSummary>()
            //    .HasOne(ps => ps.GeneratedBy)//un rezumat e generat de un user
            //    .WithMany(u => u.GeneratedSummaries)//un user poate genera mai multe rezumate
            //    .HasForeignKey(ps => ps.GeneratedById)
            //    .OnDelete(DeleteBehavior.Cascade);//daca stergem un user stergem si rezumatele lui

            
        }

    }
}
