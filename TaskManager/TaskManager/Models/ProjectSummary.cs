using System;
using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    public class ProjectSummary
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string Content { get; set; } = default!;
    }
}
