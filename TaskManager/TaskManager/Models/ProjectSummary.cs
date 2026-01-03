namespace TaskManager.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    public class ProjectSummary
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public string GeneratedById { get; set; } = default!;
        public ApplicationUser GeneratedBy { get; set; } = default!;

        [Required]
        public string Content { get; set; } = default!;
    }
}
