using TaskManager.Models;
using TaskManager.ViewModels.Comments;
using Microsoft.AspNetCore.Mvc.Rendering;

// alias explicit ca sa nu se mai bata cap in cap cu System.Threading.Tasks.TaskStatus
using TaskStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.ViewModels.TaskItems
{
    public class TaskDetailsViewModel
    {
        public TaskItem Task { get; set; } = default!;
        public List<Comment> Comments { get; set; } = new();
        public string CurrentUserId { get; set; } = string.Empty;

        public string? CurrentAssigneeId { get; set; }
        public string? CurrentAssigneeLabel { get; set; }

        public bool CanAssign { get; set; }
        public bool CanChangeStatus { get; set; }

        public TaskAssignViewModel AssignForm { get; set; } = new();
        public TaskStatusUpdateViewModel StatusForm { get; set; } = new();

        public CommentFormViewModel NewComment { get; set; } = new();
    }

    public class TaskAssignViewModel
    {
        public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public string SelectedUserId { get; set; } = string.Empty;
        public List<SelectListItem> MemberOptions { get; set; } = new();
    }

    public class TaskStatusUpdateViewModel
    {
        public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public TaskStatus NewStatus { get; set; }
    }
}
