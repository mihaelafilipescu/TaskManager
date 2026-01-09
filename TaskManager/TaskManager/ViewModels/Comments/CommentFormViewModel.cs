using System.ComponentModel.DataAnnotations;

namespace TaskManager.ViewModels.Comments
{
    public class CommentFormViewModel
    {
        // Asta imi spune la ce task se leaga comentariul
        public int TaskId { get; set; }

        // Cerinta: textul sa nu fie gol
        [Required(ErrorMessage = "Comment text cannot be empty.")]
        [StringLength(1000, MinimumLength = 1, ErrorMessage = "Comment text cannot be empty.")]
        public string Text { get; set; } = string.Empty;
    }
}
