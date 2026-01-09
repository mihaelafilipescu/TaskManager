using System.Collections.Generic;

namespace TaskManager.ViewModels.Home
{
    public class HomeIndexViewModel
    {
        public int? LatestProjectId { get; set; }
        public List<ProjectNavItem> Projects { get; set; } = new();
    }

    public class ProjectNavItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }
}
