using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using StudentAPI.Services;

namespace StudentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;

        public UserController(AppDbContext context, JwtService jwtService, EmailService emailService)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
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
                    CreatedAt = DateTime.UtcNow,
                    RegisterNumber = request.RegisterNumber
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
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                
                if (user == null)
                    return NotFound(new { message = "User not found" });

                if (!user.IsApproved)
                    return StatusCode(403, new { message = "blocked" });

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
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    name = $"{user.FirstName} {user.LastName}",
                    registerNumber = user.RegisterNumber ?? "",
                    profilePhotoType = string.IsNullOrEmpty(user.ProfilePhotoType) ? "none" : user.ProfilePhotoType,
                    profileAvatarIndex = user.ProfileAvatarIndex
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
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    name = $"{user.FirstName} {user.LastName}",
                    email = user.Email,
                    role = user.Role,
                    registerNumber = user.RegisterNumber ?? ""
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPut("update-name")]
        public IActionResult UpdateName([FromQuery] string email, [FromBody] UpdateNameRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email is required" });

                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                
                if (user == null)
                    return NotFound(new { message = "User not found" });

                user.FirstName = request.FirstName;
                user.LastName = request.LastName;

                _context.Users.Update(user);
                _context.SaveChanges();

                return Ok(new
                {
                    message = "Name updated successfully",
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    name = $"{user.FirstName} {user.LastName}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // FORGOT PASSWORD - Step 1: Send OTP
        // ==========================================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                if (user == null)
                    return NotFound(new { status = "not_found", message = "No account found with this email" });

                // Generate 6-digit OTP
                var otp = new Random().Next(100000, 999999).ToString();

                // Save OTP and expiry (10 minutes)
                user.ResetOtp = otp;
                user.ResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
                _context.Users.Update(user);
                _context.SaveChanges();

                // Send email
                await _emailService.SendOtpEmailAsync(
                    user.Email,
                    otp,
                    $"{user.FirstName} {user.LastName}"
                );

                return Ok(new { status = "sent", message = "OTP sent to your email" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        // ==========================================
        // FORGOT PASSWORD - Step 2: Verify OTP
        // ==========================================
        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                if (user == null)
                    return NotFound(new { status = "not_found", message = "User not found" });

                if (string.IsNullOrEmpty(user.ResetOtp) || user.ResetOtpExpiry == null)
                    return BadRequest(new { status = "no_otp", message = "No OTP request found. Please request a new OTP." });

                if (DateTime.UtcNow > user.ResetOtpExpiry)
                    return BadRequest(new { status = "expired", message = "OTP has expired. Please request a new one." });

                if (user.ResetOtp != request.Otp)
                    return BadRequest(new { status = "invalid", message = "Invalid OTP code" });

                return Ok(new { status = "verified", message = "OTP verified successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        // ==========================================
        // FORGOT PASSWORD - Step 3: Reset Password
        // ==========================================
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Email == request.Email);
                if (user == null)
                    return NotFound(new { status = "not_found", message = "User not found" });

                if (string.IsNullOrEmpty(user.ResetOtp) || user.ResetOtpExpiry == null)
                    return BadRequest(new { status = "no_otp", message = "No OTP request found" });

                if (DateTime.UtcNow > user.ResetOtpExpiry)
                    return BadRequest(new { status = "expired", message = "OTP has expired" });

                if (user.ResetOtp != request.Otp)
                    return BadRequest(new { status = "invalid", message = "Invalid OTP code" });

                // Update password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

                // Clear OTP fields
                user.ResetOtp = null;
                user.ResetOtpExpiry = null;

                _context.Users.Update(user);
                _context.SaveChanges();

                return Ok(new { status = "success", message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }

        // ==========================================
        // PROFILE PHOTO - Get
        // ==========================================
        [HttpGet("profile-photo")]
        public IActionResult GetProfilePhoto([FromQuery] string email)
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
                    type = string.IsNullOrEmpty(user.ProfilePhotoType) ? "none" : user.ProfilePhotoType,
                    avatarIndex = user.ProfileAvatarIndex,
                    hasImage = !string.IsNullOrEmpty(user.ProfilePhotoFileName)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // PROFILE PHOTO - Set (avatar or remove)
        // ==========================================
        [HttpPut("profile-photo")]
        public IActionResult SetProfilePhoto([FromQuery] string email, [FromBody] ProfilePhotoRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email is required" });

                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                user.ProfilePhotoType = request.Type ?? "none";
                user.ProfileAvatarIndex = request.AvatarIndex;

                // If setting to avatar or none, clear any uploaded image
                if (request.Type != "image" && !string.IsNullOrEmpty(user.ProfilePhotoFileName))
                {
                    var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "profile_photos", user.ProfilePhotoFileName);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                    user.ProfilePhotoFileName = null;
                }

                _context.Users.Update(user);
                _context.SaveChanges();

                return Ok(new { message = "Profile photo updated", type = user.ProfilePhotoType, avatarIndex = user.ProfileAvatarIndex });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // PROFILE PHOTO - Upload image
        // ==========================================
        [HttpPost("profile-photo/upload")]
        public async Task<IActionResult> UploadProfilePhoto([FromQuery] string email, IFormFile file)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email is required" });

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file provided" });

                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Create directory
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "profile_photos");
                Directory.CreateDirectory(uploadDir);

                // Delete old file if exists
                if (!string.IsNullOrEmpty(user.ProfilePhotoFileName))
                {
                    var oldPath = Path.Combine(uploadDir, user.ProfilePhotoFileName);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // Save new file
                var fileName = $"{user.Id}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                user.ProfilePhotoType = "image";
                user.ProfilePhotoFileName = fileName;
                user.ProfileAvatarIndex = 0;

                _context.Users.Update(user);
                _context.SaveChanges();

                return Ok(new { message = "Profile photo uploaded", type = "image" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ==========================================
        // PROFILE PHOTO - Serve image
        // ==========================================
        [HttpGet("profile-photo/image")]
        public IActionResult GetProfileImage([FromQuery] string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return BadRequest(new { message = "Email is required" });

                var user = _context.Users.FirstOrDefault(u => u.Email == email);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                if (string.IsNullOrEmpty(user.ProfilePhotoFileName))
                    return NotFound(new { message = "No profile image" });

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "profile_photos", user.ProfilePhotoFileName);
                if (!System.IO.File.Exists(filePath))
                    return NotFound(new { message = "Image file not found" });

                var bytes = System.IO.File.ReadAllBytes(filePath);
                return File(bytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class ProfilePhotoRequest
    {
        public string? Type { get; set; } = "none";
        public int AvatarIndex { get; set; } = 0;
    }
}
