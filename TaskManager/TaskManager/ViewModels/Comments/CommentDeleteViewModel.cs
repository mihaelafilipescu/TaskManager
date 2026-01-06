namespace TaskManager.ViewModels.Comments
{
    public class CommentDeleteViewModel
    {
        // Pastrez id-urile ca sa stiu ce sterg si unde ma intorc dupa.
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int ProjectId { get; set; }

        // Astea sunt doar pentru afisare in confirm delete.
        public string Text { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
    }
}
