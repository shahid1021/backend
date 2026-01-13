using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StudentAPI.Controllers
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(int userId, string email, string role)
        {
            var jwtSettings = _configuration.GetSection("Jwt");

            var claims = new[]
{
    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
    new Claim(JwtRegisteredClaimNames.Email, email),
    new Claim(ClaimTypes.Role, role),
    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
};


            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"])
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    Convert.ToDouble(jwtSettings["DurationInMinutes"])
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        //internal string GenerateToken(int userId, string email, object role)
        //{
        //    throw new NotImplementedException();
        //}

        //internal string GenerateToken(int userId, string? role)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
