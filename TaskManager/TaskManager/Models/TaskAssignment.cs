namespace TaskManager.Models
{
    using System;
    public class TaskAssignment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        public TaskItem TaskItem { get; set; }

        public string UserId { get; set; } = default!;
        public ApplicationUser User { get; set; } = default!;

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public string AssignedById { get; set; } = default!;
        public ApplicationUser AssignedBy { get; set; } = default!;
    }
}
