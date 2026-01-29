using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet]
        public IActionResult Get()
        {
            var projects = _context.Projects.ToList();
            return Ok(projects);
        }

        [HttpGet("{projectId}/files")]
        [Authorize]
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

        [HttpPost("{projectId}/upload")]
        [Authorize]
        public async Task<IActionResult> UploadFile(int projectId, [FromForm] IFormFile File)
        {
            try
            {
                if (File == null || File.Length == 0)
                    return BadRequest(new { error = "No file uploaded" });

                // Get user ID from JWT token
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { error = "User not found" });
                
                int userId = int.Parse(userIdClaim.Value);

                // Create upload directory if it doesn't exist
                var uploadPath = Path.Combine(_environment.ContentRootPath, "Uploads", "Projects", projectId.ToString());
                Directory.CreateDirectory(uploadPath);

                // Generate unique filename but keep structure: GUID_originalname.ext
                var originalFileName = File.FileName;
                var fileName = $"{Guid.NewGuid()}_{originalFileName}";
                var filePath = Path.Combine(uploadPath, fileName);

                // Save file to disk
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await File.CopyToAsync(stream);
                }

                // Save file info to database
                var projectFile = new StudentAPI.Models.ProjectFile
                {
                    ProjectId = projectId,
                    FileName = fileName,
                    OriginalFileName = originalFileName,
                    FileSize = File.Length,
                    FilePath = filePath,
                    UploadedBy = userId,
                    UploadedAt = DateTime.UtcNow
                };

                _context.ProjectFiles.Add(projectFile);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    message = "File uploaded successfully",
                    fileName = fileName,
                    projectId = projectId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpDelete("{projectId}/files/{fileName}")]
        [Authorize]
        public IActionResult DeleteFile(int projectId, string fileName)
        {
            try
            {
                // Find file in database
                var fileRecord = _context.ProjectFiles
                    .FirstOrDefault(f => f.ProjectId == projectId && f.FileName == fileName);

                if (fileRecord == null)
                    return NotFound(new { error = "File not found" });

                // Delete physical file
                if (System.IO.File.Exists(fileRecord.FilePath))
                {
                    System.IO.File.Delete(fileRecord.FilePath);
                }

                // Delete from database
                _context.ProjectFiles.Remove(fileRecord);
                _context.SaveChanges();

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{projectId}/files/{fileName}/download")]
        [Authorize]
        public IActionResult DownloadFile(int projectId, string fileName)
        {
            try
            {
                // Find file in database
                var fileRecord = _context.ProjectFiles
                    .FirstOrDefault(f => f.ProjectId == projectId && f.FileName == fileName);

                if (fileRecord == null)
                    return NotFound(new { error = "File not found" });

                if (!System.IO.File.Exists(fileRecord.FilePath))
                    return NotFound(new { error = "Physical file not found" });

                var fileBytes = System.IO.File.ReadAllBytes(fileRecord.FilePath);
                
                return File(fileBytes, "application/pdf", fileRecord.OriginalFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("{projectId}/sync-files")]
        [Authorize]
        public IActionResult SyncExistingFiles(int projectId)
        {
            try
            {
                var uploadPath = Path.Combine(_environment.ContentRootPath, "Uploads", "Projects", projectId.ToString());
                
                if (!Directory.Exists(uploadPath))
                    return Ok(new { message = "No files to sync" });

                var existingFiles = Directory.GetFiles(uploadPath);
                var syncedCount = 0;

                foreach (var filePath in existingFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    
                    // Check if already in database
                    var exists = _context.ProjectFiles.Any(f => f.FileName == fileName);
                    if (exists) continue;

                    // Extract original name (remove GUID prefix)
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
                        UploadedBy = 1, // Default user
                        UploadedAt = fileInfo.CreationTime
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

