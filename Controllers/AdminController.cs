using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ==================== DASHBOARD STATS ====================
        [HttpGet("stats")]
        public IActionResult GetDashboardStats()
        {
            try
            {
                var totalUsers = _context.Users.Count();
                var totalStudents = _context.Users.Count(u => u.Role == "Student");
                var totalTeachers = _context.Users.Count(u => u.Role == "Teacher");
                var totalProjects = _context.Projects.Count();
                var completedProjects = _context.Projects.Count(p => p.Status == "Completed");
                var ongoingProjects = totalProjects - completedProjects;
                var totalFiles = _context.ProjectFiles.Count();
                var totalNotifications = _context.TeacherNotifications.Count();

                // Recent registrations (last 7 days)
                var recentUsers = _context.Users
                    .Where(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .Count();

                return Ok(new
                {
                    totalUsers,
                    totalStudents,
                    totalTeachers,
                    totalProjects,
                    completedProjects,
                    ongoingProjects,
                    totalFiles,
                    totalNotifications,
                    recentUsers
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ALL USERS ====================
        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            try
            {
                var users = _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new
                    {
                        u.Id,
                        u.FirstName,
                        u.LastName,
                        u.Email,
                        u.Role,
                        u.IsApproved,
                        u.CreatedAt
                    })
                    .ToList();

                return Ok(new { count = users.Count, users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ADD USER ====================
        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] AdminCreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
                    return BadRequest(new { error = "Email and Password are required" });

                var existing = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                if (existing != null)
                    return BadRequest(new { error = "Email already exists" });

                var user = new User
                {
                    FirstName = request.FirstName ?? "",
                    LastName = request.LastName ?? "",
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = request.Role ?? "Student",
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow,
                    RegisterNumber = request.RegisterNumber
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return Ok(new
                {
                    message = "User created successfully",
                    user = new
                    {
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.Role,
                        user.IsApproved,
                        user.RegisterNumber,
                        user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== UPDATE USER ROLE ====================
        [HttpPut("users/{id}/role")]
        public IActionResult UpdateUserRole(int id, [FromBody] UpdateRoleRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                user.Role = request.Role;
                _context.SaveChanges();

                return Ok(new { message = "Role updated successfully", role = user.Role });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== TOGGLE USER APPROVAL ====================
        [HttpPut("users/{id}/approve")]
        public IActionResult ToggleUserApproval(int id)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                user.IsApproved = !user.IsApproved;
                _context.SaveChanges();

                return Ok(new { message = user.IsApproved ? "User approved" : "User blocked", isApproved = user.IsApproved });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== DELETE USER ====================
        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == id);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                _context.Users.Remove(user);
                _context.SaveChanges();

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ALL PROJECTS WITH FILES ====================
        [HttpGet("projects")]
        public IActionResult GetAllProjects()
        {
            try
            {
                var projects = _context.Projects
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        p.ProjectId,
                        p.Title,
                        p.Description,
                        p.Abstraction,
                        p.Status,
                        p.CreatedBy,
                        p.Batch,
                        p.TeamMembers,
                        p.TeacherId,
                        p.CreatedAt,
                        p.DateCompleted,
                        fileCount = _context.ProjectFiles.Count(f => f.ProjectId == p.ProjectId)
                    })
                    .ToList();

                return Ok(new { count = projects.Count, projects });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== DELETE PROJECT ====================
        [HttpDelete("projects/{id}")]
        public IActionResult DeleteProject(int id)
        {
            try
            {
                var project = _context.Projects.FirstOrDefault(p => p.ProjectId == id);
                if (project == null)
                    return NotFound(new { message = "Project not found" });

                // Also delete associated files
                var files = _context.ProjectFiles.Where(f => f.ProjectId == id).ToList();
                _context.ProjectFiles.RemoveRange(files);
                _context.Projects.Remove(project);
                _context.SaveChanges();

                return Ok(new { message = "Project and associated files deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ALL NOTIFICATIONS ====================
        [HttpGet("notifications")]
        public IActionResult GetAllNotifications()
        {
            try
            {
                var notifications = _context.TeacherNotifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.Id,
                        n.Message,
                        n.TeacherName,
                        n.TeacherEmail,
                        n.CreatedAt
                    })
                    .ToList();

                return Ok(new { count = notifications.Count, notifications });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== DELETE NOTIFICATION ====================
        [HttpDelete("notifications/{id}")]
        public IActionResult DeleteNotification(int id)
        {
            try
            {
                var notification = _context.TeacherNotifications.FirstOrDefault(n => n.Id == id);
                if (notification == null)
                    return NotFound(new { message = "Notification not found" });

                _context.TeacherNotifications.Remove(notification);
                _context.SaveChanges();

                return Ok(new { message = "Notification deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ADMIN: ADD PROJECT WITH FILE ====================
        [HttpPost("projects/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AdminUploadProject(
            [FromForm(Name = "File")] IFormFile? file,
            [FromForm] string? title,
            [FromForm] string? description,
            [FromForm] string? abstraction,
            [FromForm] string? batch,
            [FromForm] string? createdBy,
            [FromForm] string? teamMembers
        )
        {
            try
            {
                // Create the project
                var project = new Project
                {
                    Title = title ?? "Untitled Project",
                    Description = description ?? "",
                    Abstraction = abstraction ?? "",
                    Status = "Completed",
                    CreatedBy = createdBy ?? "Previous Student",
                    Batch = batch ?? DateTime.Now.Year.ToString(),
                    TeamMembers = teamMembers ?? "",
                    TeacherId = 0,
                    CreatedAt = DateTime.UtcNow,
                    DateCompleted = DateTime.UtcNow
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                // Upload file if provided
                if (file != null && file.Length > 0)
                {
                    var uploadPath = Path.Combine(
                        _environment.ContentRootPath,
                        "Uploads",
                        "Projects",
                        project.ProjectId.ToString()
                    );
                    Directory.CreateDirectory(uploadPath);

                    var originalFileName = file.FileName;
                    var fileName = $"{Guid.NewGuid()}_{originalFileName}";
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var projectFile = new ProjectFile
                    {
                        ProjectId = project.ProjectId,
                        FileName = fileName,
                        OriginalFileName = originalFileName,
                        FileSize = file.Length,
                        FilePath = filePath,
                        UploadedBy = 0,
                        UploadedAt = DateTime.UtcNow
                    };

                    _context.ProjectFiles.Add(projectFile);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Project added successfully", projectId = project.ProjectId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==================== ADMIN: ADD FILE TO EXISTING PROJECT ====================
        [HttpPost("projects/{projectId}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> AdminUploadFileToProject(
            int projectId,
            [FromForm(Name = "File")] IFormFile file
        )
        {
            try
            {
                var project = _context.Projects.FirstOrDefault(p => p.ProjectId == projectId);
                if (project == null)
                    return NotFound(new { message = "Project not found" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file uploaded" });

                var uploadPath = Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "Projects",
                    projectId.ToString()
                );
                Directory.CreateDirectory(uploadPath);

                var originalFileName = file.FileName;
                var fileName = $"{Guid.NewGuid()}_{originalFileName}";
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var projectFile = new ProjectFile
                {
                    ProjectId = projectId,
                    FileName = fileName,
                    OriginalFileName = originalFileName,
                    FileSize = file.Length,
                    FilePath = filePath,
                    UploadedBy = 0,
                    UploadedAt = DateTime.UtcNow
                };

                _context.ProjectFiles.Add(projectFile);
                await _context.SaveChangesAsync();

                return Ok(new { message = "File uploaded successfully", fileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // ==================== REQUEST MODELS ====================
    public class UpdateRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class AdminCreateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Role { get; set; }
        public string? RegisterNumber { get; set; }
    }
}
