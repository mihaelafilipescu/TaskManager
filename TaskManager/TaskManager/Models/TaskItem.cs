using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;


namespace TaskManager.Models
{
    public enum TaskStatus {
        [Display(Name = "Not Started")]
        NotStarted,
        [Display(Name = "In Progress")]
        InProgress,
        [Display(Name = "Completed")]
        Completed 
    }
    public enum MediaType { Text, Image, Video }

    public class TaskItem : IValidatableObject
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [BindNever, ValidateNever]
        public Project Project { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field."), StringLength(200)]
        public string Title { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field."), StringLength(2000)]
        public string Description { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field.")]
        public TaskStatus Status { get; set; } = TaskStatus.NotStarted;

        [Required(ErrorMessage = "This is a required field.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "This is a required field.")]
        public DateTime EndDate { get; set; }

        [Required]
        public MediaType MediaType { get; set; } = MediaType.Text;

        [Required, StringLength(4000)]
        public string MediaContent { get; set; } = default!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BindNever, ValidateNever]
        public string CreatedById { get; set; } = default!;

        [BindNever, ValidateNever]
        public ApplicationUser CreatedBy { get; set; } = default!;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public ICollection<TaskAssignment> Assignments { get; set; } = new List<TaskAssignment>();


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate <= StartDate)
            {
                yield return new ValidationResult(
                    "Data de finalizare trebuie să fie mai mare decât data de început.",
                    new[] { nameof(EndDate) }
                );
            }
        }
    }
}
