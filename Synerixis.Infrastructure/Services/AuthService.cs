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

        public string GenerateJwt(Guid userId, string userType, Guid? shopId = null)
        {
            Console.WriteLine($"GenerateJwt: userId={userId}, userType={userType}, shopId={shopId}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("userType", userType),                    // 前端用
                new Claim("role", userType),                       // 后端用（简单字符串）
                new Claim("uid", userId.ToString())
            };

            if (shopId.HasValue)
            {
                claims.Add(new Claim("shopId", shopId.Value.ToString()));
            }

            // 为了向后兼容，如果角色是Seller，我们额外添加一个sellerId claim
            if (userType.Equals("Seller", StringComparison.OrdinalIgnoreCase))
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