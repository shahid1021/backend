using System;

namespace StudentAPI.Models
{
    public class TeacherNotification
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TeacherName { get; set; } = string.Empty;
        public string TeacherEmail { get; set; } = string.Empty;
        
        /// <summary>
        /// The User.Id of the teacher who sent this notification.
        /// 0 means it was sent by Admin (broadcast to all students).
        /// </summary>
        public int TeacherId { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
