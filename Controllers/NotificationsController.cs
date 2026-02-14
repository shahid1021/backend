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
        /// Send a notification message from teacher to all students
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

                var notification = new TeacherNotification
                {
                    Message = request.Message,
                    TeacherName = request.TeacherName ?? "Teacher",
                    TeacherEmail = request.TeacherEmail ?? "teacher@school.edu",
                    CreatedAt = DateTime.UtcNow
                };

                _context.TeacherNotifications.Add(notification);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Notification saved: {notification.Message}");

                return Ok(new
                {
                    success = true,
                    message = "Notification sent to all students",
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
        /// Get all notifications for students
        /// GET /api/notifications/get
        /// </summary>
        [HttpGet("get")]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var notifications = _context.TeacherNotifications
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
                        timestamp = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
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
        /// Delete a notification (admin only)
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
