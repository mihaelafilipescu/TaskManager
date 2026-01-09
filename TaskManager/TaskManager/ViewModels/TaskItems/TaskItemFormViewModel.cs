using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskManager.Models;
using TaskStatusEnum = TaskManager.Models.TaskStatus;
using MediaTypeEnum = TaskManager.Models.MediaType;


namespace TaskManager.ViewModels.TaskItems
{
    // Aici tin datele pentru formularul de Create/Edit Task
    // + validari server-side (profesorii tin mult la asta)
    public class TaskItemFormViewModel : IValidatableObject
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        [Required(ErrorMessage = "This is a required field.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
        public string Title { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field.")]
        [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        public string Description { get; set; } = default!;

        [Required(ErrorMessage = "This is a required field.")]
        public TaskStatusEnum Status { get; set; } = TaskStatusEnum.NotStarted;

        [Required(ErrorMessage = "This is a required field.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "This is a required field.")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        // Aici tin minte cui e asignat task-ul direct din Create/Edit
        // Il folosesc doar ca input (UserId) si validez in controller ca apartine proiectului
        [Required(ErrorMessage = "Please select an assignee.")]
        public string SelectedAssigneeId { get; set; } = string.Empty;

        // Aici incarc lista de useri din proiect (pentru dropdown)
        // Nu e camp obligatoriu la POST, e doar pentru UI
        public List<SelectListItem> AssigneeOptions { get; set; } = new();

        [Required(ErrorMessage = "This is a required field.")]
        public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Text;

        // Aici tin textul (pentru Text) sau URL-ul (pentru Video) sau path-ul deja salvat (pentru Image)
        public string? MediaContent { get; set; }

        // Aici vine fisierul uploadat cand MediaType = Image
        public IFormFile? ImageFile { get; set; }

        // Aici tin path-ul vechi cand editez, ca sa nu pierd imaginea daca nu incarc alta
        public string? ExistingImagePath { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Aici validez regula importanta: end date >= start date
            // O fac aici ca sa fie server-side, nu doar in UI
            if (EndDate.Date < StartDate.Date)
            {
                yield return new ValidationResult(
                    "End date must be after or equal to start date.",
                    new[] { nameof(EndDate) }
                );
            }

            // Aici ma asigur ca enum-urile sunt valori valide (siguranta extra)
            if (!Enum.IsDefined(typeof(TaskStatusEnum), Status))
            {
                yield return new ValidationResult(
                    "Invalid task status.",
                    new[] { nameof(Status) }
                );
            }

            if (!Enum.IsDefined(typeof(MediaTypeEnum), MediaType))
            {
                yield return new ValidationResult(
                    "Invalid media type.",
                    new[] { nameof(MediaType) }
                );
            }

            // Aici validez media in functie de tip (Text / Video / Image)
            if (MediaType == MediaType.Text)
            {
                // Pentru Text: trebuie continut minim decent
                if (string.IsNullOrWhiteSpace(MediaContent) || MediaContent.Trim().Length < 5)
                {
                    yield return new ValidationResult(
                        "Please add text content (min 5 characters).",
                        new[] { nameof(MediaContent) }
                    );
                }
            }
            else if (MediaType == MediaType.Video)
            {
                // Pentru Video: trebuie URL valid
                if (string.IsNullOrWhiteSpace(MediaContent))
                {
                    yield return new ValidationResult(
                        "Please add a video URL (YouTube/Vimeo).",
                        new[] { nameof(MediaContent) }
                    );
                }
                else if (!Uri.TryCreate(MediaContent, UriKind.Absolute, out var uri))
                {
                    yield return new ValidationResult(
                        "The video URL is not valid.",
                        new[] { nameof(MediaContent) }
                    );
                }
                else
                {
                    // Optional: accept doar youtube/vimeo, ca sa nu bage orice link random
                    var host = (uri.Host ?? "").ToLowerInvariant();
                    var ok =
                        host.Contains("youtube.com") ||
                        host.Contains("youtu.be") ||
                        host.Contains("vimeo.com");

                    if (!ok)
                    {
                        yield return new ValidationResult(
                            "Only YouTube or Vimeo links are allowed.",
                            new[] { nameof(MediaContent) }
                        );
                    }
                }
            }
            else if (MediaType == MediaType.Image)
            {
                // Pentru Image:
                // - la Create: trebuie obligatoriu ImageFile
                // - la Edit: accept daca exista ExistingImagePath si nu incarc alt fisier
                var hasExisting = !string.IsNullOrWhiteSpace(ExistingImagePath);

                if (ImageFile == null && !hasExisting)
                {
                    yield return new ValidationResult(
                        "Please upload an image.",
                        new[] { nameof(ImageFile) }
                    );
                }

                if (ImageFile != null)
                {
                    // Verific tipul de fisier (sa fie imagine)
                    if (string.IsNullOrWhiteSpace(ImageFile.ContentType) || !ImageFile.ContentType.StartsWith("image/"))
                    {
                        yield return new ValidationResult(
                            "Only image files are allowed.",
                            new[] { nameof(ImageFile) }
                        );
                    }

                    // Verific marimea fisierului (max 2MB)
                    const long maxBytes = 2 * 1024 * 1024;
                    if (ImageFile.Length > maxBytes)
                    {
                        yield return new ValidationResult(
                            "Image must be max 2MB.",
                            new[] { nameof(ImageFile) }
                        );
                    }
                }
            }
        }
    }
}
