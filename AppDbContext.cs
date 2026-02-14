using Microsoft.EntityFrameworkCore;
using StudentAPI.Models;

namespace StudentAPI
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectFile> ProjectFiles { get; set; }
        public DbSet<TeacherNotification> TeacherNotifications { get; set; }
    }
}
