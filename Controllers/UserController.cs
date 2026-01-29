using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;

        public UserController(AppDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { message = "UserController is working!", timestamp = DateTime.UtcNow });
        }

        [HttpGet("all")]
        public IActionResult GetAllUsers()
        {
            try
            {
                var users = _context.Users.Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Role,
                    u.IsApproved,
                    u.CreatedAt
                }).ToList();

                return Ok(new { count = users.Count, users = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("register")]
        public IActionResult Register(RegisterModel request)
        {
            try
            {
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                if (existingUser != null)
                    return BadRequest(new { status = "exists" });

                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = "Student",
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return Ok(new { status = "success" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public IActionResult Login(UserLogin request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email && u.IsApproved);
                
                if (user == null)
                    return NotFound(new { message = "User not found" });

                if (string.IsNullOrEmpty(user.PasswordHash))
                    return StatusCode(500, new { message = "Password hash missing" });

                bool isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                
                if (!isValid)
                    return Unauthorized(new { message = "Wrong password" });

                string token = _jwtService.GenerateToken(user.Id, user.Email, user.Role);

                return Ok(new
                {
                    token = token,
                    role = user.Role,
                    name = user.FirstName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("me")]
        public IActionResult Me([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email is required" });

                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                
                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(new
                {
                    name = user.FirstName,
                    email = user.Email,
                    role = user.Role
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
