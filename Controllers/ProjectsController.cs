//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
////using Microsoft.Data.SqlClient;
//using System.Security.Claims;
//using Microsoft.AspNetCore.Http;
//using System.IO;
//using System.Threading.Tasks;
//using StudentAPI.Models;

//namespace StudentAPI.Controllers
//{
//    [ApiController]
//    [Route("api/[controller]")]
//    public class ProjectsController : ControllerBase
//    {
//        private readonly IConfiguration _configuration;

//        public ProjectsController(IConfiguration configuration)
//        {
//            _configuration = configuration;
//        }

//        // ================= STUDENT: GET MY PROJECT =================
//        //[Authorize(Roles = "Student")]
//        [HttpGet("my")]
//        public IActionResult GetMyProject()
//        {
//            var userId =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
//                User.FindFirst(ClaimTypes.Name)?.Value;

//            using SqlConnection con = new SqlConnection(
//                _configuration.GetConnectionString("DefaultConnection")
//            );

//            con.Open();

//            string query = @"
//                SELECT p.ProjectId, p.Title, p.Status
//                FROM Projects p
//                INNER JOIN StudentProjects sp ON p.ProjectId = sp.ProjectId
//                WHERE sp.StudentId = @StudentId";

//            SqlCommand cmd = new SqlCommand(query, con);
//            cmd.Parameters.AddWithValue("@StudentId", userId);

//            using SqlDataReader reader = cmd.ExecuteReader();

//            if (!reader.Read())
//                return Ok(null);

//            return Ok(new
//            {
//                projectId = reader["ProjectId"],
//                title = reader["Title"],
//                status = reader["Status"]
//            });
//        }

//        // ================= STUDENT: UPLOAD PDF =================
//        [Authorize(Roles = "Student")]
//        [HttpPost("{projectId}/upload")]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> UploadProjectFile(
//        int projectId,
//        [FromForm] ProjectFileUploadRequest request)
//        {
//            if (request == null)
//                return BadRequest("Request is null");

//            if (request.File == null)
//                return BadRequest("Request.File is null");

//            var file = request.File;

//            if (file.Length == 0)
//                return BadRequest("File length is zero");


//            // Allow only PDF
//            //if (!file.ContentType.Contains("pdf"))
//            //    return BadRequest("Only PDF files allowed");
//            var extension = Path.GetExtension(file.FileName).ToLower();

//            if (extension != ".pdf")
//                return BadRequest("Only PDF files allowed");


//            var userId =
//                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
//                User.FindFirst(ClaimTypes.Name)?.Value;

//            // Create upload directory
//            var uploadRoot = Path.Combine(
//                Directory.GetCurrentDirectory(),
//                "Uploads",
//                "Projects",
//                projectId.ToString()
//            );

//            Directory.CreateDirectory(uploadRoot);

//            // Generate unique file name
//            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
//            var filePath = Path.Combine(uploadRoot, uniqueFileName);

//            // Save file to disk
//            using (var stream = new FileStream(filePath, FileMode.Create))
//            {
//                await file.CopyToAsync(stream);
//            }

//            // Save file record in DB
//            using SqlConnection con = new SqlConnection(
//                _configuration.GetConnectionString("DefaultConnection")
//            );
//            con.Open();

//            string insertQuery = @"
//                INSERT INTO ProjectFiles
//                (ProjectId, FileName, FilePath, UploadedBy)
//                VALUES
//                (@ProjectId, @FileName, @FilePath, @UploadedBy)";

//            SqlCommand cmd = new SqlCommand(insertQuery, con);
//            cmd.Parameters.AddWithValue("@ProjectId", projectId);
//            cmd.Parameters.AddWithValue("@FileName", file.FileName);
//            cmd.Parameters.AddWithValue("@FilePath", filePath);
//            cmd.Parameters.AddWithValue("@UploadedBy", userId);

//            cmd.ExecuteNonQuery();

//            return Ok(new { message = "PDF uploaded successfully" });
//        }
//    }
//}
using Microsoft.AspNetCore.Mvc;
using StudentAPI;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var projects = _context.Projects.ToList();
            return Ok(projects);
        }
    }
}
