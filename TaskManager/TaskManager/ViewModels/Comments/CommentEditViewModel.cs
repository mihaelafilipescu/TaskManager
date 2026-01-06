using System.ComponentModel.DataAnnotations;

namespace TaskManager.ViewModels.Comments
{
    public class CommentEditViewModel
    {
        // Id-ul comentariului (pe asta il editez).
        public int Id { get; set; }

        // Am nevoie de TaskId si ProjectId ca sa ma intorc corect in pagina de Details.
        public int TaskId { get; set; }
        public int ProjectId { get; set; }

        [Required]
        public string Text { get; set; } = string.Empty;
    }
}
