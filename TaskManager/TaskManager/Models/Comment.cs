namespace TaskManager.Models
{
    using System;
    using System.ComponentModel.DataAnnotations;
    public class Comment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        public Task Task { get; set; } = default!;

        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        [Required]
        public string Text { get; set; } = default!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
