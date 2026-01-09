using System.ComponentModel.DataAnnotations;

namespace TaskManager.ViewModels.Projects
{
    public class ProjectCreateVm
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [Required]
        public string Description { get; set; } = "";
    }
}
