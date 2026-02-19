using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentAPI.Models;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/teacher-projects")]
    public class TeacherProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TeacherProjectsController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/teacher-projects?teacherEmail=xxx
        [HttpGet]
        public IActionResult GetByTeacher([FromQuery] string teacherEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(teacherEmail))
                    return BadRequest(new { error = "teacherEmail is required" });

                var teacher = _context.Users.FirstOrDefault(u => u.Email == teacherEmail);
                if (teacher == null)
                    return NotFound(new { error = "Teacher not found" });

                var projects = _context.Projects
                    .Where(p => p.TeacherId == teacher.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new
                    {
                        id = p.ProjectId,
                        groupNumber = p.GroupNumber ?? "",
                        groupMembers = p.TeamMembers ?? "",
                        projectName = p.Title ?? "",
                        completionStages = string.IsNullOrEmpty(p.CompletionStages)
                            ? new List<bool> { false, false, false, false, false, false, false, false, false, false }
                            : System.Text.Json.JsonSerializer.Deserialize<List<bool>>(p.CompletionStages),
                        status = p.Status,
                        createdAt = p.CreatedAt
                    })
                    .ToList();

                return Ok(projects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST /api/teacher-projects
        [HttpPost]
        public IActionResult Create([FromBody] CreateTeacherProjectRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TeacherEmail))
                    return BadRequest(new { error = "teacherEmail is required" });

                var teacher = _context.Users.FirstOrDefault(u => u.Email == request.TeacherEmail);
                if (teacher == null)
                    return NotFound(new { error = "Teacher not found" });

                var project = new Project
                {
                    Title = request.ProjectName,
                    Description = "",
                    Abstraction = "",
                    Status = "Ongoing",
                    CreatedBy = teacher.FirstName + " " + teacher.LastName,
                    Batch = "",
                    TeamMembers = request.GroupMembers,
                    GroupNumber = request.GroupNumber,
                    CompletionStages = System.Text.Json.JsonSerializer.Serialize(
                        new List<bool> { false, false, false, false, false, false, false, false, false, false }),
                    TeacherId = teacher.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Projects.Add(project);
                _context.SaveChanges();

                return Ok(new
                {
                    id = project.ProjectId,
                    groupNumber = project.GroupNumber,
                    groupMembers = project.TeamMembers,
                    projectName = project.Title,
                    completionStages = new List<bool> { false, false, false, false, false, false, false, false, false, false },
                    status = project.Status,
                    createdAt = project.CreatedAt
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // PUT /api/teacher-projects/{id}/stages
        [HttpPut("{id}/stages")]
        public IActionResult UpdateStages(int id, [FromBody] UpdateStagesRequest request)
        {
            try
            {
                var project = _context.Projects.FirstOrDefault(p => p.ProjectId == id);
                if (project == null)
                    return NotFound(new { error = "Project not found" });

                project.CompletionStages = System.Text.Json.JsonSerializer.Serialize(request.CompletionStages);

                // Check if all stages are completed
                bool allCompleted = request.CompletionStages.All(s => s);
                project.Status = allCompleted ? "Completed" : "Ongoing";
                if (allCompleted && project.DateCompleted == null)
                    project.DateCompleted = DateTime.UtcNow;
                else if (!allCompleted)
                    project.DateCompleted = null;

                _context.SaveChanges();

                return Ok(new { message = "Stages updated", status = project.Status });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // DELETE /api/teacher-projects/{id}
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            try
            {
                var project = _context.Projects.FirstOrDefault(p => p.ProjectId == id);
                if (project == null)
                    return NotFound(new { error = "Project not found" });

                _context.Projects.Remove(project);
                _context.SaveChanges();

                return Ok(new { message = "Project deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // Request models
    public class CreateTeacherProjectRequest
    {
        public string? TeacherEmail { get; set; }
        public string? GroupNumber { get; set; }
        public string? GroupMembers { get; set; }
        public string? ProjectName { get; set; }
    }

    public class UpdateStagesRequest
    {
        public List<bool> CompletionStages { get; set; } = new();
    }
}
