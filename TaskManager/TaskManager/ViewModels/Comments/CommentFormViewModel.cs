using System.ComponentModel.DataAnnotations;

namespace TaskManager.ViewModels.Comments
{
    public class CommentFormViewModel
    {
        // TaskId imi spune la ce task se ataseaza comentariul.
        public int TaskId { get; set; }

        // Pun Required ca sa prind validarea si server-side.
        [Required]
        public string Text { get; set; } = string.Empty;
    }
}
