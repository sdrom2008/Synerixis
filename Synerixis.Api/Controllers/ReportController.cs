using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System.Security.Claims;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize] // 登录用户均可访问，但数据会按角色过滤
    public class ReportsController : BaseApiController
    {
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return userId;
            return null;
        }

        private string? GetCurrentRole()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role");
            return roleClaim?.Value;
        }

        // ============================================
        // 1. Dashboard 数据聚合
        // ============================================
        /// <summary>
        /// 获取Dashboard关键指标（今日/本周/本月）
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            // 根据角色确定查询范围
            var userRole = GetCurrentRole();
            Guid? shopId = null;

            if (userRole == "Seller")
            {
                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == userId.Value);
                if (seller == null) return NotFound("Seller not found");
                shopId = seller.Id; // 假设Seller.Id作为店铺ID（实际可能是单独的Shop表）
            }
            else if (userRole == "Agent" || userRole == "Supervisor")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                shopId = agent.ShopId;
            }
            else
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Invalid role" });
            }

            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = todayStart.AddDays(-(int)now.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // 1. 今日对话统计
            var todayConversations = await _db.ChatSessions
                .Where(s => s.ShopId == shopId && s.CreatedAt >= todayStart)
                .CountAsync();

            var todayResolved = await _db.ChatSessions
                .Where(s => s.ShopId == shopId && s.Status == SessionStatus.Resolved && s.ResolvedAt >= todayStart)
                .CountAsync();

            // 2. AI解决率（今日）
            var todayAiOnly = await _db.ChatSessions
                .Where(s => s.ShopId == shopId && s.CreatedAt >= todayStart && s.AssignedAgentId == null)
                .CountAsync();

            var aiResolutionRate = todayConversations > 0 ? (double)todayAiOnly / todayConversations * 100 : 0;

            // 3. 客服绩效（今日）
            var todayAgentStats = await _db.AgentStats
                .Include(s => s.Agent)
                .Where(s => s.Agent != null && s.Agent.ShopId == shopId && s.StatDate == todayStart)
                .Select(s => new
                {
                    AgentId = s.AgentId,
                    AgentName = s.Agent!.Name,
                    s.TotalConversations,
                    s.AvgResponseTimeSeconds,
                    s.ResolutionRate
                })
                .ToListAsync();

            // 4. 总资产
            // 假设Seller有FreeQuota字段表示剩余对话额度
            var totalQuota = 0;
            if (userRole == "Seller")
            {
                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == userId.Value);
                if (seller != null) totalQuota = seller.FreeQuota ?? 0;
            }

            return Ok(new
            {
                period = "today",
                conversations = new
                {
                    total = todayConversations,
                    resolved = todayResolved,
                    aiOnly = todayAiOnly,
                    aiResolutionRate = Math.Round(aiResolutionRate, 2)
                },
                agentPerformance = todayAgentStats,
                subscription = new
                {
                    remainingQuota = totalQuota
                },
                generatedAt = now
            });
        }

        // ============================================
        // 2. 客服绩效报表
        // ============================================
        /// <summary>
        /// 获取客服绩效统计（按日期范围、按店铺）
        /// </summary>
        [HttpGet("agent-performance")]
        public async Task<IActionResult> GetAgentPerformance(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? shopId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var userRole = GetCurrentRole();

            // 权限控制：
            // - Supervisor: 只能看自己的店铺
            // - Admin: 可以看所有店铺（shopId为空时）
            // - Seller: 只能看自己店铺的数据

            Guid? currentShopId = null;
            if (userRole == "Seller")
            {
                currentShopId = userId; // 假设Seller.Id就是店铺ID
            }
            else if (userRole == "Agent" || userRole == "Supervisor")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                currentShopId = agent.ShopId;
            }

            // 如果传了shopId参数，验证权限
            if (shopId.HasValue)
            {
                if (currentShopId.HasValue && shopId.Value != currentShopId.Value)
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cannot view other shops' data" });
            }
            else
            {
                shopId = currentShopId;
            }

            if (!startDate.HasValue) startDate = DateTime.UtcNow.AddDays(-7);
            if (!endDate.HasValue) endDate = DateTime.UtcNow;

            var stats = await _db.AgentStats
                .Include(s => s.Agent)
                .Where(s => s.Agent != null && s.StatDate >= startDate && s.StatDate <= endDate && s.Agent.ShopId == shopId)
                .OrderByDescending(s => s.StatDate)
                .ThenBy(s => s.Agent!.Name)
                .Select(s => new
                {
                    Date = s.StatDate,
                    AgentName = s.Agent!.Name,
                    AgentRole = s.Agent!.Role,
                    s.TotalConversations,
                    s.ResolvedCount,
                    ResolutionRate = Math.Round(s.ResolutionRate * 100, 2),
                    s.AvgResponseTimeSeconds,
                    s.AvgSatisfaction,
                    s.TotalMessages,
                    s.AgentMessages,
                    s.AiMessages
                })
                .ToListAsync();

            return Ok(new
            {
                startDate,
                endDate,
                shopId,
                totalRecords = stats.Count,
                stats
            });
        }

        // ============================================
        // 3. 对话量趋势报表
        // ============================================
        /// <summary>
        /// 按日期统计对话量趋势
        /// </summary>
        [HttpGet("conversation-trend")]
        public async Task<IActionResult> GetConversationTrend(
            [FromQuery] string? period = "day", // day, month
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var userRole = GetCurrentRole();
            Guid? shopId = null;

            if (userRole == "Seller")
            {
                shopId = userId;
            }
            else if (userRole == "Agent" || userRole == "Supervisor")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                shopId = agent.ShopId;
            }

            if (!startDate.HasValue) startDate = DateTime.UtcNow.AddDays(-30);
            if (!endDate.HasValue) endDate = DateTime.UtcNow;

            var query = _db.ChatSessions
                .Where(s => s.ShopId == shopId && s.CreatedAt >= startDate && s.CreatedAt <= endDate);

            List<object> trendData;

            if (period == "day")
            {
                trendData = await query
                    .GroupBy(s => s.CreatedAt.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        total = g.Count(),
                        resolved = g.Count(s => s.Status == SessionStatus.Resolved),
                        aiOnly = g.Count(s => s.AssignedAgentId == null)
                    })
                    .OrderBy(x => x.date)
                    .Cast<object>()
                    .ToListAsync();
            }
            else // month
            {
                trendData = await query
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
                    .Select(g => new
                    {
                        month = $"{g.Key.Year}-{g.Key.Month:D2}",
                        total = g.Count(),
                        resolved = g.Count(s => s.Status == SessionStatus.Resolved)
                    })
                    .OrderBy(x => x.month)
                    .Cast<object>()
                    .ToListAsync();
            }

            return Ok(new
            {
                period,
                startDate,
                endDate,
                data = trendData
            });
        }
    }
}
