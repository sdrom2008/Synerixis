using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Synerixis.Application.Interfaces;

namespace Synerixis.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;

        public AuthService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateJwt(Guid userId, string role)
        {
            Console.WriteLine($"GenerateJwt: userId={userId}, role={role}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role), // 使用标准的Role Claim
                new Claim("uid", userId.ToString()) // 自定义一个uid，方便前端解析
            };

            // 为了向后兼容，如果角色是Seller，我们额外添加一个sellerId claim
            if (role.Equals("Seller", StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim("sellerId", userId.ToString()));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiryMinutes = double.Parse(_config["Jwt:ExpiryMinutes"] ?? "1440");
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}