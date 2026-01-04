using System.ComponentModel.DataAnnotations;

namespace TaskManager.ViewModels.Projects
{
    public class ProjectMembersVm
    {
        public int ProjectId { get; set; }
        public string ProjectTitle { get; set; } = "";

        public bool IsOrganizer { get; set; }
        public string OrganizerId { get; set; } = "";


        // lista membri
        public List<MemberItemVm> Members { get; set; } = new();

        // input pentru adaugare
        [Required]
        [Display(Name = "User (email or username)")]
        public string UserIdentifier { get; set; } = "";
    }

    public class MemberItemVm
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
