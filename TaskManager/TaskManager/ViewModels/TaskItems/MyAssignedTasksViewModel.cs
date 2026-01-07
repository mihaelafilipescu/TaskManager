using System.Collections.Generic;
using TaskManager.Models;

// alias ca sa nu se bata cu System.Threading.Tasks.TaskStatus
using TaskItemStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.ViewModels.TaskItems
{
    public class MyAssignedTasksViewModel
    {
        public List<TaskItem> Tasks { get; set; } = new();

        public TaskItemStatus? StatusFilter { get; set; }
    }
}
