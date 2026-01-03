namespace TaskManager.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = default!;

        [Required]
        public string Description { get; set; } = default!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // FK catre organizator
        public string OrganizerId { get; set; } = default!;
        public ApplicationUser Organizer { get; set; } = default!;

        // relatii
        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
        public ICollection<Task> Tasks { get; set; } = new List<Task>();
        public ICollection<ProjectSummary> Summaries { get; set; } = new List<ProjectSummary>();
    }
}
