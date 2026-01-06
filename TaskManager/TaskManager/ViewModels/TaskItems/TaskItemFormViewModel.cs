using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using TaskManager.Models;

namespace TaskManager.ViewModels.TaskItems
{
    public class TaskItemFormViewModel
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required(ErrorMessage = "This is a required field."), StringLength(200)]
        public string Title { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field."), StringLength(2000)]
        public string Description { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field.")]
        public TaskManager.Models.TaskStatus Status { get; set; } = TaskManager.Models.TaskStatus.NotStarted;

        [Required(ErrorMessage = "This is a required field.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "This is a required field.")]
        public DateTime EndDate { get; set; }

        [Required]
        public MediaType MediaType { get; set; } = MediaType.Text;

        // Aici tin textul (pentru Text) sau URL-ul (pentru Video) sau path-ul deja salvat (pentru Image)
        public string? MediaContent { get; set; }

        // Aici vine fisierul uploadat cand MediaType = Image
        public IFormFile? ImageFile { get; set; }

        // Aici tin path-ul vechi cand editez, ca sa nu pierd imaginea daca nu incarc alta
        public string? ExistingImagePath { get; set; }
    }
}
