using Back_End.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Back_End.Services
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateAccessToken(User user)
        {
            var secretKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key chưa được cấu hình");

            var issuer = _configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("JWT Issuer chưa được cấu hình");

            var audience = _configuration["Jwt:Audience"]
                ?? throw new InvalidOperationException("JWT Audience chưa được cấu hình");

            var expiryMinutes = _configuration.GetValue<int>(
            "Jwt:ExpiryMinutes", 30);


            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, 
                user.Id ?? throw new InvalidOperationException("User ID missing")),

                new Claim(JwtRegisteredClaimNames.Email, user.Email),

                new Claim("displayName", user.DisplayName),

                new Claim(
                    JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)
            );

            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public string HashRefreshToken(string refreshToken)
        {
            return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        }
    }
}
