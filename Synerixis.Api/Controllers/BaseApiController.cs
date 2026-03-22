using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Synerixis.Api.Helpers;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Api.Controllers
{
    public class BaseApiController : ControllerBase
    {
        protected IActionResult HandleError(Exception ex)
        {
            return StatusCode(500, new { message = "服务器内部错误", error = ex.Message });
        }

        /// <summary>
        /// 从 JWT 提取当前用户信息（不查询数据库）
        /// </summary>
        protected CurrentUser GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            var userTypeClaim = User.FindFirst("userType") ?? User.FindFirst("role");
            var shopIdClaim = User.FindFirst("shopId");

            if (userIdClaim == null || userTypeClaim == null)
                throw new UnauthorizedAccessException("Token 无效");

            return new CurrentUser
            {
                UserId = Guid.Parse(userIdClaim.Value),
                UserType = userTypeClaim.Value,
                ShopId = shopIdClaim != null ? Guid.Parse(shopIdClaim.Value) : null
            };
        }

        /// <summary>
        /// 确保当前用户是 Seller，并返回其 ShopId
        /// </summary>
        protected Guid GetCurrentSellerShopId()
        {
            var current = GetCurrentUser();
            if (!current.IsSeller)
                throw new UnauthorizedAccessException("仅商户可访问");
            if (!current.ShopId.HasValue)
                throw new UnauthorizedAccessException("商户未绑定店铺");
            return current.ShopId.Value;
        }

        /// <summary>
        /// 获取当前 Agent（客服或主管）
        /// </summary>
        protected Agent GetCurrentAgent(AppDbContext db)
        {
            var current = GetCurrentUser();
            if (!current.IsAgent && !current.IsSupervisor)
                throw new UnauthorizedAccessException("仅客服或主管可访问");

            var agent = db.Agents.FirstOrDefault(a => a.Id == current.UserId);
            if (agent == null)
                throw new UnauthorizedAccessException("Agent 不存在");

            return agent;
        }
    }
}
