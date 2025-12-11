namespace TaskManager.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public enum TaskStatus { NotStarted, InProgress, Completed }
    public enum MediaType { None, Text, Image, Video }

    public class Task
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = default!;

        [Required]
        public string Description { get; set; } = default!;

        public TaskStatus Status { get; set; } = TaskStatus.NotStarted;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public MediaType MediaType { get; set; } = MediaType.None;
        public string? MediaUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedById { get; set; } = default!;
        public ApplicationUser CreatedBy { get; set; } = default!;

        public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
