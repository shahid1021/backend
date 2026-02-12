namespace StudentAPI.Models
{
    public class Project
    {
        public int ProjectId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Abstraction { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public string? Batch { get; set; }
        public string? TeamMembers { get; set; }
        public int TeacherId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DateCompleted { get; set; }
    }
}
