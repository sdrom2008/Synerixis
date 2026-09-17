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

        /// <param name="userType">Seller | Agent | Supervisor | Admin。
        /// Admin 仅表示 PLATFORM Agents.Role=Admin（[Authorize(Roles="Admin")]），
        /// 与商家工作台「店长」无关；商家团队不得签发此角色。</param>
        public string GenerateJwt(Guid userId, string userType, Guid? shopId = null)
        {
            return WriteToken(userId, userType, shopId, support: false, supportAdminId: null, expiryMinutesOverride: null);
        }

        public string GenerateSupportJwt(Guid sellerShopId, Guid adminActorId, int expiryMinutes = 60)
        {
            // Acts as Seller for target shop (full-shop inbox) without a merchant Agent login.
            return WriteToken(
                sellerShopId,
                "Seller",
                sellerShopId,
                support: true,
                supportAdminId: adminActorId,
                expiryMinutesOverride: Math.Clamp(expiryMinutes, 5, 240));
        }

        private string WriteToken(
            Guid userId,
            string userType,
            Guid? shopId,
            bool support,
            Guid? supportAdminId,
            double? expiryMinutesOverride)
        {
            Console.WriteLine($"GenerateJwt: userId={userId}, userType={userType}, shopId={shopId}, support={support}");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("userId", userId.ToString()),
                new Claim("uid", userId.ToString()),
                new Claim("userType", userType),
                // ASP.NET [Authorize(Roles=...)] 默认读 ClaimTypes.Role；Admin → 平台 /api/admin/*
                new Claim(ClaimTypes.Role, userType),
                new Claim("role", userType),
            };

            // Seller: ChatSession.ShopId == Seller.Id; always emit shopId for merchant APIs
            if (userType.Equals("Seller", StringComparison.OrdinalIgnoreCase))
            {
                var sellerShopId = shopId ?? userId;
                claims.Add(new Claim("shopId", sellerShopId.ToString()));
                claims.Add(new Claim("sellerId", userId.ToString()));
            }
            else if (shopId.HasValue)
            {
                claims.Add(new Claim("shopId", shopId.Value.ToString()));
            }

            if (support)
            {
                claims.Add(new Claim("support", "true"));
                claims.Add(new Claim("impersonation", "true"));
                if (supportAdminId.HasValue)
                    claims.Add(new Claim("supportAdminId", supportAdminId.Value.ToString()));
            }

            var jwtKey = _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
                jwtKey = "dev-secret-key-please-change-in-production";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expiryMinutes = expiryMinutesOverride
                ?? double.Parse(_config["Jwt:ExpiryMinutes"] ?? "1440");
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
