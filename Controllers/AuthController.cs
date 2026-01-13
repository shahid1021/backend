using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;




namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly JwtService _jwtService;   // JWT service for token generation
        private object role;
        private string storedHash;
        //private object _context;

        public AuthController(IConfiguration configuration, JwtService jwtService)
        {
            _configuration = configuration;
            _jwtService = jwtService;
        }

            

        // -------- REGISTER --------
        [HttpPost("register")]
        public IActionResult Register(RegisterRequest request)
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection")
            );

            con.Open();

            string checkQuery = "SELECT COUNT(*) FROM dbo.Users WHERE Email=@Email";
            SqlCommand checkCmd = new SqlCommand(checkQuery, con);
            checkCmd.Parameters.AddWithValue("@Email", request.Email);

            int exists = (int)checkCmd.ExecuteScalar();
            if (exists > 0)
                return BadRequest(new { status = "exists" });


            string insertQuery = @"
                INSERT INTO dbo.Users
                (FirstName, LastName, Email, PasswordHash, Role, IsApproved)
                VALUES
                (@FirstName, @LastName, @Email, @Password, 'Student', 1)
            ";

            SqlCommand insertCmd = new SqlCommand(insertQuery, con);
            insertCmd.Parameters.AddWithValue("@FirstName", request.FirstName);
            insertCmd.Parameters.AddWithValue("@LastName", request.LastName);
            insertCmd.Parameters.AddWithValue("@Email", request.Email);
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            insertCmd.Parameters.AddWithValue("@Password", hashedPassword);


            insertCmd.ExecuteNonQuery();

            return Ok(new { status = "success" });

        }

        // -------- LOGIN --------
        //[HttpPost("login")]
        //public IActionResult Login(LoginRequest request)
        //{
        //    if (request == null)
        //        return BadRequest("Request body is missing");

        //    //var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);

        //    if (user == null)
        //        return NotFound("User not found");

        //    if (string.IsNullOrWhiteSpace(user.Password))
        //        return StatusCode(500, "User password is NULL in database");

        //    bool isPasswordValid = BCrypt.Net.BCrypt.Verify(
        //        request.Password,
        //        user.Password
        //    );

        //    if (!isPasswordValid)
        //        return Unauthorized("Wrong password");

        //    return Ok(new
        //    {
        //        token = "dummy-token-for-now",
        //        role = user.Role,
        //        name = user.FirstName
        //    });
        //}
        [HttpPost("login")]
        public IActionResult Login(LoginRequest request)
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection")
            );

            con.Open();

            string query = @"
        SELECT Id, FirstName, Email, PasswordHash, Role
        FROM dbo.Users
        WHERE Email = @Email AND IsApproved = 1
    ";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Email", request.Email);

            using SqlDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound("User not found");

            string storedHash = reader["PasswordHash"]?.ToString();

            if (string.IsNullOrEmpty(storedHash))
                return StatusCode(500, "Password hash missing in database");

            bool isValid = BCrypt.Net.BCrypt.Verify(request.Password, storedHash);

            if (!isValid)
                return Unauthorized("Wrong password");

            int userId = Convert.ToInt32(reader["Id"]);
            string role = reader["Role"].ToString();
            string name = reader["FirstName"].ToString();

            // ✅ generate REAL JWT
            string email = reader["Email"].ToString();

            string token = _jwtService.GenerateToken(userId, email, role);


            return Ok(new
            {
                token = token,
                role = role,
                name = name
            });

        }








        // -------- PROFILE (ME) --------
        //[Authorize]
        [HttpGet("me")]
        public IActionResult Me([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest("Email is required");

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection")
            );

            con.Open();

            string query = @"
        SELECT FirstName, LastName, Email, Role
        FROM dbo.Users
        WHERE Email = @Email
    ";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@Email", email);

            using SqlDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound("User not found");

            return Ok(new
            {
                name = reader["FirstName"].ToString(),
                email = reader["Email"].ToString(),
                role = reader["Role"].ToString()
            });
        }




        // -------- REQUEST MODELS --------
        public class RegisterRequest
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class LoginRequest
        {
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}
