namespace StudentAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? RegisterNumber { get; set; }

        // Forgot Password OTP fields
        public string? ResetOtp { get; set; }
        public DateTime? ResetOtpExpiry { get; set; }

        // Profile Photo fields
        public string ProfilePhotoType { get; set; } = "none"; // none, avatar, image
        public int ProfileAvatarIndex { get; set; } = 0;
        public string? ProfilePhotoFileName { get; set; }
    }
}
