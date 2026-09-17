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
    [Route("api/support")]
    [Authorize(Roles = "Supervisor,Admin")] // 只有主管和管理员可以访问
    public class AgentController : BaseApiController
    {
        private readonly AppDbContext _db;

        public AgentController(AppDbContext db)
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

        // ============================================
        // 1. 获取客服列表
        // ============================================
        /// <summary>
        /// 获取店铺下的客服列表（主管查看）
        /// </summary>
        [HttpGet("agents")]
        public async Task<IActionResult> GetAgents()
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var currentAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (currentAgent == null) return NotFound("Agent not found");

            // 只能查看自己店铺的客服
            var agents = await _db.Agents
                .Where(a => a.ShopId == currentAgent.ShopId)
                .OrderBy(a => a.Role) // Supervisor在前，Agent在后
                .ThenBy(a => a.Name)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Email,
                    a.Role,
                    a.IsActive,
                    a.IsOnline,
                    a.MaxConcurrentSessions,
                    a.CurrentSessionCount,
                    a.LastLoginAt,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(agents);
        }

        // ============================================
        // 2. 获取单个客服绩效
        // ============================================
        /// <summary>
        /// 获取指定客服的绩效统计（按日期）
        /// </summary>
        [HttpGet("agents/{agentId}/stats")]
        public async Task<IActionResult> GetAgentStats(
            Guid agentId,
            [FromQuery] DateTime? date = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var currentAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (currentAgent == null) return NotFound("Agent not found");

            // 验证 agent 属于同一店铺
            var targetAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
            if (targetAgent == null) return NotFound("Agent not found");
            if (targetAgent.ShopId != currentAgent.ShopId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Cannot view stats of agents from other shops" });

            var statDate = date?.Date ?? DateTime.UtcNow.Date;

            // 尝试从数据库获取当日统计
            var stat = await _db.AgentStats
                .FirstOrDefaultAsync(s => s.AgentId == agentId && s.StatDate == statDate);

            if (stat == null)
            {
                // 如果还没有统计数据，返回空数据或计算
                return Ok(new
                {
                    agentId,
                    statDate,
                    message = "No stats for this date yet"
                });
            }

            return Ok(new
            {
                stat.AgentId,
                stat.StatDate,
                stat.TotalConversations,
                stat.PendingCount,
                stat.ActiveCount,
                stat.ResolvedCount,
                stat.ClosedCount,
                stat.AvgResponseTimeSeconds,
                stat.AvgFirstResponseTimeSeconds,
                stat.TotalMessages,
                stat.AgentMessages,
                stat.AiMessages,
                stat.SatisfactionCount,
                stat.AvgSatisfaction,
                stat.ResolutionRate
            });
        }

        // ============================================
        // 3. 获取质检对话列表
        // ============================================
        /// <summary>
        /// 获取需要质检的对话（通常为已解决的会话）
        /// </summary>
        [HttpGet("quality")]
        public async Task<IActionResult> GetQualitySessions(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var currentAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (currentAgent == null) return NotFound("Agent not found");

            var query = _db.ChatSessions
                .Include(s => s.AssignedAgent)
                .Where(s => s.ShopId == currentAgent.ShopId && s.Status == SessionStatus.Resolved);

            if (startDate.HasValue)
                query = query.Where(s => s.ResolvedAt >= startDate);
            if (endDate.HasValue)
                query = query.Where(s => s.ResolvedAt <= endDate);

            var total = await query.CountAsync();
            var sessions = await query
                .OrderByDescending(s => s.ResolvedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.SessionId,
                    s.CustomerId,
                    s.CustomerName,
                    s.AssignedAgentId,
                    AgentName = s.AssignedAgent != null ? s.AssignedAgent.Name : "Unknown",
                    s.ResolvedAt,
                    s.Satisfaction,
                    s.ResolutionTime,
                    s.MessageCount,
                    s.AgentMessageCount,
                    s.AiMessageCount
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, sessions });
        }

        // ============================================
        // 4. 提交质检评分
        // ============================================
        /// <summary>
        /// 为已解决的会话提交质检评分
        /// </summary>
        [HttpPost("quality/{sessionId}/review")]
        public async Task<IActionResult> SubmitQualityReview(
            string sessionId,
            [FromBody] QualityReviewDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var currentAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (currentAgent == null) return NotFound("Agent not found");

            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.ShopId == currentAgent.ShopId);

            if (session == null) return NotFound("Session not found");
            if (session.Status != SessionStatus.Resolved)
                return BadRequest("Only resolved sessions can be reviewed");

            if (dto.Satisfaction.HasValue && dto.Satisfaction >= 1 && dto.Satisfaction <= 5)
            {
                session.SetSatisfaction((byte)dto.Satisfaction.Value);
                await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Review submitted successfully" });
        }

        // ============================================
        // 5. 手动分配会话（主管分配）
        // ============================================
        /// <summary>
        /// 主管手动将会话分配给指定客服
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignSession(
            [FromBody] AssignSessionDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var currentAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (currentAgent == null) return NotFound("Agent not found");

            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == dto.SessionId && s.ShopId == currentAgent.ShopId);

            if (session == null) return NotFound("Session not found");
            if (session.Status != SessionStatus.Pending)
                return BadRequest($"Cannot assign a session with status {session.Status}");

             var targetAgent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == dto.AgentId && a.ShopId == currentAgent.ShopId);
            if (targetAgent == null || !dto.AgentId.HasValue) return BadRequest("Target agent not found or not in same shop");

            session.AssignToAgent(dto.AgentId.Value);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Session assigned successfully",
                session = new
                {
                    session.SessionId,
                    agentId = session.AssignedAgentId,
                    session.AssignedAt
                }
            });
        }
    }

    public class QualityReviewDto
    {
        public int? Satisfaction { get; set; } // 1-5
        public string? Notes { get; set; }     // 质检备注
    }

    public class AssignSessionDto
    {
        public string SessionId { get; set; } = null!;
        public Guid? AgentId { get; set; }
    }
}
