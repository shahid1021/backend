namespace StudentAPI.Models
{
    public class Project
    {
        public int ProjectId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public int TeacherId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
