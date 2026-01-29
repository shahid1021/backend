namespace StudentAPI.Models
{
    public class ProjectFile
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
