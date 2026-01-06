using System;
using System.Collections.Generic;

// alias ca sa fie clar ca folosim enum-ul nostru, nu System.Threading.Tasks.TaskStatus
using TaskItemStatus = TaskManager.Models.TaskStatus;

namespace TaskManager.ViewModels.Dashboard
{
    public class DashboardProjectItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsOrganizer { get; set; }
    }

    public class DashboardTaskItemVm
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public TaskItemStatus Status { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Aici tin cate zile mai sunt pana la deadline (poate fi negativ daca e overdue)
        public int DaysUntilDue { get; set; }

        public string? AssigneeLabel { get; set; }
        public bool IsAssignedToMe { get; set; }
    }

    public class DashboardFiltersVm
    {
        public int? ProjectId { get; set; }
        public TaskItemStatus? Status { get; set; }

        public bool DueSoonOnly { get; set; }

        // Aici tin cate zile vrea userul sa vada pana la deadline
        public int? DueInDays { get; set; }
    }

    public class DashboardIndexViewModel
    {
        public DashboardFiltersVm Filters { get; set; } = new DashboardFiltersVm();

        public List<DashboardProjectItemVm> Projects { get; set; } = new List<DashboardProjectItemVm>();
        public List<DashboardTaskItemVm> Tasks { get; set; } = new List<DashboardTaskItemVm>();

        public Dictionary<TaskItemStatus, List<DashboardTaskItemVm>> TasksByStatus { get; set; }
            = new Dictionary<TaskItemStatus, List<DashboardTaskItemVm>>();

        public List<DashboardTaskItemVm> UpcomingDeadlines { get; set; } = new List<DashboardTaskItemVm>();
    }
}
