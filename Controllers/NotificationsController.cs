using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Send a notification message.
        /// - If teacherEmail belongs to a Teacher user, TeacherId is set to that teacher's User.Id
        ///   (notification only visible to students under that teacher).
        /// - If teacherEmail belongs to an Admin user (or is empty), TeacherId = 0
        ///   (broadcast to ALL students).
        /// POST /api/notifications/send
        /// </summary>
        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Message cannot be empty" });
                }

                int teacherId = 0; // default = admin broadcast
                string senderName = request.TeacherName ?? "Admin";

                if (!string.IsNullOrEmpty(request.TeacherEmail))
                {
                    var sender = _context.Users.FirstOrDefault(u => u.Email == request.TeacherEmail);
                    if (sender != null)
                    {
                        senderName = $"{sender.FirstName} {sender.LastName}".Trim();
                        if (sender.Role == "Teacher")
                        {
                            teacherId = sender.Id;
                        }
                        // Admin role → teacherId stays 0 (broadcast)
                    }
                }

                var notification = new TeacherNotification
                {
                    Message = request.Message,
                    TeacherName = senderName,
                    TeacherEmail = request.TeacherEmail ?? "",
                    TeacherId = teacherId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TeacherNotifications.Add(notification);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Notification saved (TeacherId={teacherId}): {notification.Message}");

                return Ok(new
                {
                    success = true,
                    message = teacherId == 0
                        ? "Notification sent to all students"
                        : "Notification sent to your students",
                    notificationId = notification.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending notification: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get notifications with optional scoping.
        /// 
        /// Query params:
        ///   ?registerNumber=xxx  → Student view: returns only notifications from their teacher + admin broadcasts (TeacherId==0)
        ///   ?teacherEmail=xxx    → Teacher view: returns only notifications sent by that teacher
        ///   (no params)          → Admin view: returns ALL notifications
        ///   
        /// GET /api/notifications/get
        /// </summary>
        [HttpGet("get")]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] string? registerNumber,
            [FromQuery] string? teacherEmail)
        {
            try
            {
                IQueryable<TeacherNotification> query = _context.TeacherNotifications;

                // ── Student view: show notifications from their teacher(s) + admin broadcasts ──
                if (!string.IsNullOrEmpty(registerNumber))
                {
                    // Find all projects where this student is a team member
                    var studentProjects = _context.Projects
                        .Where(p => p.TeamMembers != null && p.TeamMembers.Contains(registerNumber))
                        .Select(p => p.TeacherId)
                        .Distinct()
                        .ToList();

                    // Filter: notifications from those teachers OR admin broadcasts (TeacherId == 0)
                    query = query.Where(n => studentProjects.Contains(n.TeacherId) || n.TeacherId == 0);
                }
                // ── Teacher view: show only their own notifications ──
                else if (!string.IsNullOrEmpty(teacherEmail))
                {
                    var teacher = _context.Users.FirstOrDefault(u => u.Email == teacherEmail);
                    if (teacher != null)
                    {
                        var tid = teacher.Id;
                        query = query.Where(n => n.TeacherId == tid);
                    }
                    else
                    {
                        return Ok(new { success = true, count = 0, notifications = new List<object>() });
                    }
                }
                // ── Admin view: no filter, return everything ──

                var notifications = query
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    count = notifications.Count,
                    notifications = notifications.Select(n => new
                    {
                        id = n.Id,
                        message = n.Message,
                        senderName = n.TeacherName,
                        timestamp = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        teacherId = n.TeacherId
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching notifications: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete a notification
        /// DELETE /api/notifications/{id}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var notification = _context.TeacherNotifications.Find(id);
                if (notification == null)
                {
                    return NotFound(new { error = "Notification not found" });
                }

                _context.TeacherNotifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Notification deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class SendNotificationRequest
    {
        public string Message { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }
    }
}
