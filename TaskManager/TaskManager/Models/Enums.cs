using System.ComponentModel.DataAnnotations;

namespace TaskManager.Models
{
    public enum TaskStatus
    {
        [Display(Name = "Not Started")]
        NotStarted = 0,

        [Display(Name = "In Progress")]
        InProgress = 1,

        [Display(Name = "Completed")]
        Completed = 2
    }

    public enum MediaType
    {
        Text = 0,
        Image = 1,
        Video = 2
    }
}
