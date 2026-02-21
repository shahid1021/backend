using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using StudentAPI;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProjectsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // --------------------
        // GET ALL PROJECTS
        // --------------------
        [HttpGet]
        public IActionResult Get()
        {
            var projects = _context.Projects.ToList();
            return Ok(projects);
        }

        // --------------------
        // GET COMPLETED PROJECTS
        // --------------------
        [HttpGet("completed")]
        public IActionResult GetCompletedProjects()
        {
            try
            {
                var completedProjects = _context.Projects
                    .Where(p => p.Status == "Completed")
                    .OrderByDescending(p => p.DateCompleted)
                    .Select(p => new
                    {
                        id = p.ProjectId,
                        title = p.Title,
                        abstraction = p.Abstraction,
                        description = p.Description,
                        createdBy = p.CreatedBy,
                        batch = p.Batch,
                        teamMembers = p.TeamMembers,
                        dateCompleted = p.DateCompleted,
                        status = p.Status,
                        isStudent = false
                    })
                    .ToList();

                return Ok(completedProjects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("{projectId}/files")]
        public IActionResult GetProjectFiles(int projectId)
        {
            try
            {
                var files = _context.ProjectFiles
                    .Where(f => f.ProjectId == projectId)
                    .OrderByDescending(f => f.UploadedAt)
                    .Select(f => new
                    {
                        id = f.Id,
                        fileName = f.FileName,
                        displayName = f.OriginalFileName,
                        uploadDate = f.UploadedAt,
                        size = f.FileSize
                    })
                    .ToList();

                return Ok(new { files });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // --------------------
        // GET PREVIOUS YEAR PROJECTS (Admin-uploaded, TeacherId=0)
        // --------------------
        [HttpGet("previous-year")]
        public IActionResult GetPreviousYearProjects()
        {
            try
            {
                var projects = _context.Projects
                    .Where(p => p.TeacherId == 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        id = p.ProjectId,
                        title = p.Title,
                        description = p.Description,
                        abstraction = p.Abstraction,
                        createdBy = p.CreatedBy,
                        batch = p.Batch,
                        teamMembers = p.TeamMembers,
                        status = p.Status,
                        dateCompleted = p.DateCompleted,
                        createdAt = p.CreatedAt,
                        files = _context.ProjectFiles
                            .Where(f => f.ProjectId == p.ProjectId)
                            .Select(f => new
                            {
                                id = f.Id,
                                fileName = f.FileName,
                                displayName = f.OriginalFileName,
                                size = f.FileSize,
                                uploadDate = f.UploadedAt
                            })
                            .ToList()
                    })
                    .ToList();

                return Ok(new { count = projects.Count, projects });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // --------------------
        // UPLOAD FILE
        // --------------------
        [HttpPost("{projectId}/upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile(
            int projectId,
            [FromForm(Name = "File")] IFormFile file,
            [FromForm] string? projectName,
            [FromForm] string? teamMembers,
            [FromForm] string? batch,
            [FromForm] string? createdBy
        )
        {
            try
            {
                // Check authorization header
                var authHeader = Request.Headers["Authorization"].ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                    return Unauthorized(new { error = "Missing or invalid authorization header" });

                var token = authHeader.Substring("Bearer ".Length).Trim();

                // Validate token and extract claims
                var jwtSettings = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json")
                    .Build()
                    .GetSection("Jwt");

                var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "");
                var tokenHandler = new JwtSecurityTokenHandler();

                try
                {
                    var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidateAudience = true,
                        ValidAudience = jwtSettings["Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    }, out SecurityToken validatedToken);

                    var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim == null)
                        return Unauthorized(new { error = "User ID not found in token" });

                    if (!int.TryParse(userIdClaim.Value, out int userId))
                        return Unauthorized(new { error = "Invalid user ID in token" });

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

                    // Create Project record for completed projects FIRST
                    var newProject = new StudentAPI.Models.Project
                    {
                        Title = projectName ?? "Untitled Project",
                        Description = originalFileName,
                        Abstraction = originalFileName,
                        Status = "Completed",
                        CreatedBy = createdBy ?? "Student",
                        Batch = batch ?? DateTime.Now.Year.ToString(),
                        TeamMembers = teamMembers ?? "Solo",
                        TeacherId = 1,
                        CreatedAt = DateTime.UtcNow,
                        DateCompleted = DateTime.UtcNow
                    };

                    _context.Projects.Add(newProject);
                    await _context.SaveChangesAsync();

                    // Now create ProjectFile with the actual project ID
                    var projectFile = new StudentAPI.Models.ProjectFile
                    {
                        ProjectId = newProject.ProjectId,
                        FileName = fileName,
                        OriginalFileName = originalFileName,
                        FileSize = file.Length,
                        FilePath = filePath,
                        UploadedBy = userId,
                        UploadedAt = DateTime.UtcNow
                    };

                    _context.ProjectFiles.Add(projectFile);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "File uploaded successfully",
                        fileName,
                        projectId = newProject.ProjectId
                    });
                }
                catch (SecurityTokenException ex)
                {
                    return Unauthorized(new { error = "Invalid token", details = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }

        // --------------------
        // DELETE FILE
        // --------------------
        [HttpDelete("{projectId}/files/{fileName}")]
        [Authorize]
        public IActionResult DeleteFile(int projectId, string fileName)
        {
            try
            {
                var fileRecord = _context.ProjectFiles
                    .FirstOrDefault(f => f.ProjectId == projectId && f.FileName == fileName);

                if (fileRecord == null)
                    return NotFound(new { error = "File not found" });

                if (System.IO.File.Exists(fileRecord.FilePath))
                {
                    System.IO.File.Delete(fileRecord.FilePath);
                }

                _context.ProjectFiles.Remove(fileRecord);
                _context.SaveChanges();

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // --------------------
        // DOWNLOAD FILE (Public - for previous year projects)
        // --------------------
        [HttpGet("previous-year/{projectId}/files/{fileName}/download")]
        public IActionResult DownloadPreviousYearFile(int projectId, string fileName)
        {
            try
            {
                var project = _context.Projects.FirstOrDefault(p => p.ProjectId == projectId && p.TeacherId == 0);
                if (project == null)
                    return NotFound(new { error = "Project not found or not a previous year project" });

                var fileRecord = _context.ProjectFiles
                    .FirstOrDefault(f => f.ProjectId == projectId && f.FileName == fileName);

                if (fileRecord == null)
                    return NotFound(new { error = "File not found" });

                if (!System.IO.File.Exists(fileRecord.FilePath))
                    return NotFound(new { error = "Physical file not found" });

                var fileBytes = System.IO.File.ReadAllBytes(fileRecord.FilePath);
                var contentType = "application/pdf";
                if (fileRecord.OriginalFileName != null)
                {
                    if (fileRecord.OriginalFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                        contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                    else if (fileRecord.OriginalFileName.EndsWith(".doc", StringComparison.OrdinalIgnoreCase))
                        contentType = "application/msword";
                    else if (fileRecord.OriginalFileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                        contentType = "text/plain";
                }

                return File(fileBytes, contentType, fileRecord.OriginalFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // --------------------
        // DOWNLOAD FILE
        // --------------------
        [HttpGet("{projectId}/files/{fileName}/download")]
        [Authorize]
        public IActionResult DownloadFile(int projectId, string fileName)
        {
            try
            {
                var fileRecord = _context.ProjectFiles
                    .FirstOrDefault(f => f.ProjectId == projectId && f.FileName == fileName);

                if (fileRecord == null)
                    return NotFound(new { error = "File not found" });

                if (!System.IO.File.Exists(fileRecord.FilePath))
                    return NotFound(new { error = "Physical file not found" });

                var fileBytes = System.IO.File.ReadAllBytes(fileRecord.FilePath);

                return File(fileBytes, "application/octet-stream", fileRecord.OriginalFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // --------------------
        // SYNC EXISTING FILES
        // --------------------
        [HttpPost("{projectId}/sync-files")]
        [Authorize]
        public IActionResult SyncExistingFiles(int projectId)
        {
            try
            {
                var uploadPath = Path.Combine(
                    _environment.ContentRootPath,
                    "Uploads",
                    "Projects",
                    projectId.ToString()
                );

                if (!Directory.Exists(uploadPath))
                    return Ok(new { message = "No files to sync" });

                var existingFiles = Directory.GetFiles(uploadPath);
                var syncedCount = 0;

                foreach (var filePath in existingFiles)
                {
                    var fileName = Path.GetFileName(filePath);

                    if (_context.ProjectFiles.Any(f => f.FileName == fileName))
                        continue;

                    var originalFileName = fileName.Contains('_')
                        ? fileName.Substring(fileName.IndexOf('_') + 1)
                        : fileName;

                    var fileInfo = new FileInfo(filePath);

                    var projectFile = new StudentAPI.Models.ProjectFile
                    {
                        ProjectId = projectId,
                        FileName = fileName,
                        OriginalFileName = originalFileName,
                        FileSize = fileInfo.Length,
                        FilePath = filePath,
                        UploadedBy = 1,
                        UploadedAt = fileInfo.CreationTimeUtc
                    };

                    _context.ProjectFiles.Add(projectFile);
                    syncedCount++;
                }

                _context.SaveChanges();

                return Ok(new { message = $"Synced {syncedCount} files to database" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
