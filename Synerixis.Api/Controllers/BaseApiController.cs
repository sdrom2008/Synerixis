using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Synerixis.Api.Helpers;
using Synerixis.Domain.Entities;
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
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub")
                ?? User.FindFirst("uid")
                ?? User.FindFirst("userId");
            var userTypeClaim = User.FindFirst("userType")
                ?? User.FindFirst(ClaimTypes.Role)
                ?? User.FindFirst("role");
            var shopIdClaim = User.FindFirst("shopId");

            if (userIdClaim == null || userTypeClaim == null)
                throw new UnauthorizedAccessException("Token 无效");

            return new CurrentUser
            {
                UserId = Guid.Parse(userIdClaim.Value),
                UserType = userTypeClaim.Value,
                ShopId = shopIdClaim != null && Guid.TryParse(shopIdClaim.Value, out var sid)
                    ? sid
                    : null
            };
        }

        /// <summary>
        /// 商家端收件箱等：Seller 与同店坐席均可，统一解析本店 ShopId。
        /// Seller: shopId claim 或 UserId；Agent/Supervisor/Admin: 必须有 shopId claim。
        /// </summary>
        protected Guid GetMerchantShopId()
        {
            var current = GetCurrentUser();
            if (current.IsSeller)
                return current.ShopId ?? current.UserId;

            if (current.IsStaff)
            {
                if (!current.ShopId.HasValue)
                    throw new UnauthorizedAccessException("坐席 Token 缺少 shopId");
                return current.ShopId.Value;
            }

            throw new UnauthorizedAccessException("无权访问商家工作台");
        }

        /// <summary>
        /// 仅 Seller（兼容旧调用）；收件箱请用 GetMerchantShopId。
        /// </summary>
        protected Guid GetCurrentSellerShopId()
        {
            var current = GetCurrentUser();
            if (!current.IsSeller)
                throw new UnauthorizedAccessException("仅商户可访问");
            return current.ShopId ?? current.UserId;
        }

        protected Guid GetCurrentSellerId()
        {
            var current = GetCurrentUser();
            if (!current.IsSeller)
                throw new UnauthorizedAccessException("仅商户可访问");
            return current.UserId;
        }

        /// <summary>
        /// 店铺绑定 / SellerConfig 读写：Seller→UserId；Supervisor/Admin→JWT shopId（所属 Seller.Id）；Agent→无权。
        /// </summary>
        protected Guid GetShopOwnerSellerId()
        {
            var current = GetCurrentUser();
            if (current.IsSeller)
                return current.ShopId ?? current.UserId;

            if (current.IsSupervisor || current.IsAdmin)
            {
                if (!current.ShopId.HasValue)
                    throw new UnauthorizedAccessException("坐席 Token 缺少 shopId");
                return current.ShopId.Value;
            }

            // Agent 等无权管理店铺连接 / 本店 AI 配置
            throw new UnauthorizedAccessException("仅商家或主管可管理店铺连接与 AI 设置");
        }

        /// <summary>Seller / Supervisor / Admin 可管店铺连接与 AI 设置；Agent 不可。</summary>
        protected bool CanManageShopOwnerResources()
        {
            var current = GetCurrentUser();
            return current.IsSeller || current.IsSupervisor || current.IsAdmin;
        }

        /// <summary>
        /// 团队管理：Seller 本人，或同店 Supervisor/Admin。
        /// 返回被管理店铺的 Seller.Id（= Agents.ShopId）。
        /// </summary>
        protected Guid GetTeamManagedShopId()
        {
            var current = GetCurrentUser();
            if (current.IsSeller)
                return current.ShopId ?? current.UserId;

            if (current.IsSupervisor || current.IsAdmin)
            {
                if (!current.ShopId.HasValue)
                    throw new UnauthorizedAccessException("坐席 Token 缺少 shopId");
                return current.ShopId.Value;
            }

            throw new UnauthorizedAccessException("仅商家或主管可管理团队");
        }

        protected Agent GetCurrentAgent(AppDbContext db)
        {
            var current = GetCurrentUser();
            if (!current.IsStaff)
                throw new UnauthorizedAccessException("仅客服或主管可访问");

            var agent = db.Agents.FirstOrDefault(a => a.Id == current.UserId);
            if (agent == null)
                throw new UnauthorizedAccessException("Agent 不存在");

            return agent;
        }
    }
}
