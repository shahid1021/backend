using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        [HttpGet("any-user")]
        [Authorize]
        public IActionResult AnyUser()
        {
            return Ok("Any logged-in user");
        }

        [HttpGet("teacher-only")]
        [Authorize(Roles = "Teacher")]
        public IActionResult TeacherOnly()
        {
            return Ok("Teacher access granted");
        }

        [HttpGet("student-only")]
        [Authorize(Roles = "Student")]
        public IActionResult StudentOnly()
        {
            return Ok("Student access granted");
        }
    }
}
