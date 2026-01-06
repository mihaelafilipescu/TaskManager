using TaskManager.Models;
using TaskManager.ViewModels.Comments;

namespace TaskManager.ViewModels.TaskItems
{
    public class TaskDetailsViewModel
    {
        // Task-ul pe care il afisez.
        public TaskItem Task { get; set; } = default!;

        // Comentariile pentru task (filtrate + sortate in controller).
        public List<Comment> Comments { get; set; } = new();

        // Pastrez userul curent ca sa decid in view daca apar butoanele Edit/Delete la comentarii.
        public string CurrentUserId { get; set; } = string.Empty;

        // Model separat pentru form-ul de "Add a comment".
        public CommentFormViewModel NewComment { get; set; } = new();
    }
}
