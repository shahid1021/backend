using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        // GET PROJECT FILES
        // --------------------
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

        // --------------------
        // UPLOAD FILE (FIXED FOR SWAGGER)
        // --------------------
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpPost("{projectId}/upload")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile(
            int projectId,
            [FromForm(Name = "file")] IFormFile file
        )
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { error = "No file uploaded" });

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { error = "User not found" });

                int userId = int.Parse(userIdClaim.Value);

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

                var projectFile = new StudentAPI.Models.ProjectFile
                {
                    ProjectId = projectId,
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
                    projectId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
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
