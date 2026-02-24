using System;

namespace StudentAPI.Models
{
    public class StandaloneTrendingProject
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Abstraction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Model for standalone trending projects (not linked to main Projects table)
    }
}
