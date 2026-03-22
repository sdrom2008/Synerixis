using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System.Security.Claims;
using System;
using System.Threading.Tasks;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/support")]
    [Authorize]
    public class SupportController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IAgentStatsService _statsService;

        [AllowAnonymous]
        [HttpGet("debug/init-db")]
        public async Task<IActionResult> InitDatabase()
        {
            try
            {
                await _db.Database.EnsureCreatedAsync();
                var agentCount = await _db.Agents.CountAsync();
                return Ok(new { message = "Database ensured.", initialAgentCount = agentCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to initialize database.", error = ex.Message });
            }
        }
        
        public SupportController(AppDbContext db, IAgentStatsService statsService)
        {
            _db = db;
            _statsService = statsService;
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
        // 1. 获取会话列表
        // ============================================
        [HttpGet("tickets")]
        public async Task<IActionResult> GetTickets(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var userRole = GetCurrentRole();
            var query = _db.ChatSessions
                .Include(s => s.Shop)
                .Include(s => s.AssignedAgent)
                .AsQueryable();

            if (userRole == "Agent")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                query = query.Where(s => s.ShopId == agent.ShopId);
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<SessionStatus>(status, true, out var sessionStatus))
                {
                    query = query.Where(s => s.Status == sessionStatus);
                }
                else
                {
                    return BadRequest("Invalid status parameter");
                }
            }

            query = query
                .OrderByDescending(s => s.Status == SessionStatus.Pending)
                .ThenByDescending(s => s.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.SessionId,
                    s.CustomerId,
                    s.CustomerName,
                    s.Platform,
                    Status = ((SessionStatus)s.Status).ToString(),
                    s.Priority,
                    AssignedAgent = s.AssignedAgent != null ? new
                    {
                        s.AssignedAgent.Id,
                        s.AssignedAgent.Name
                    } : null,
                    s.AssignedAt,
                    s.CreatedAt,
                    s.LastActiveAt,
                    s.MessageCount,
                    s.AiMessageCount,
                    s.AgentMessageCount
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ============================================
        // 2. 客服接管会话
        // ============================================
        [HttpPost("tickets/{sessionId}/take")]
        public async Task<IActionResult> TakeTicket(string sessionId)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return NotFound("Session not found");
            if (session.Status != SessionStatus.Pending)
                return BadRequest($"Cannot take a session with status {session.Status}");

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (agent == null) return NotFound("Agent not found");
            if (agent.ShopId != session.ShopId)
                return Forbid("You are not authorized to take this session");

            session.AssignToAgent(userId.Value);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Session taken successfully",
                session = new
                {
                    session.SessionId,
                    session.Status,
                    session.AssignedAgentId,
                    session.AssignedAt
                }
            });
        }

        // ============================================
        // 3. 客服发送回复
        // ============================================
        [HttpPost("tickets/{sessionId}/reply")]
        public async Task<IActionResult> ReplyToTicket(
            string sessionId,
            [FromBody] SupportReplyDto dto)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrEmpty(dto.Content))
                return BadRequest("Reply content cannot be empty");

            var session = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return NotFound("Session not found");
            if (session.Status != SessionStatus.Active)
                return BadRequest($"Cannot reply to a session with status {session.Status}");
            if (session.AssignedAgentId != userId.Value)
                return Forbid("You are not assigned to this session");

            var message = ChatMessage.FromAgent(dto.Content, session.Id);
            _db.ChatMessages.Add(message);

            session.AddAgentMessage();
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Reply sent successfully",
                messageId = message.Id,
                timestamp = message.Timestamp
            });
        }

        // ============================================
        // 4. 标记会话已解决
        // ============================================
        [HttpPost("tickets/{sessionId}/resolve")]
        public async Task<IActionResult> ResolveTicket(
            string sessionId,
            [FromBody] ResolveTicketDto? dto = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return NotFound("Session not found");
            if (session.Status != SessionStatus.Active)
                return BadRequest($"Cannot resolve a session with status {session.Status}");
            if (session.AssignedAgentId != userId.Value)
                return Forbid("You are not assigned to this session");

            byte? satisfaction = null;
            if (dto?.Satisfaction != null && dto.Satisfaction >= 1 && dto.Satisfaction <= 5)
            {
                satisfaction = (byte)dto.Satisfaction.Value;
            }

            session.Resolve(satisfaction);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Session resolved successfully",
                session = new
                {
                    session.SessionId,
                    session.Status,
                    session.Satisfaction,
                    session.ResolutionTime
                }
            });
        }

        // ============================================
        // 5. 获取客户信息（订单历史）
        // ============================================
        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetCustomerInfo(
            string customerId,
            [FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
            if (agent == null) return NotFound("Agent not found");

            var orders = await _db.Orders
                .Where(o => o.ShopId == agent.ShopId && o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderTime)
                .Take(limit)
                .Select(o => new
                {
                    o.OrderNo,
                    o.Status,
                    o.TotalAmount,
                    o.Platform,
                    o.OrderTime,
                    o.PaidAt,
                    o.ShippedAt,
                    o.ReceivedAt,
                    Logistics = o.LogisticsCompany + " " + o.LogisticsNo
                })
                .ToListAsync();

            var recentSessions = await _db.ChatSessions
                .Where(s => s.CustomerId == customerId && s.ShopId == agent.ShopId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => new
                {
                    s.SessionId,
                    s.Status,
                    s.CreatedAt,
                    AssignedAgentName = s.AssignedAgent != null ? s.AssignedAgent.Name : "AI"
                })
                .ToListAsync();

            return Ok(new
            {
                customerId,
                orders,
                recentSessions
            });
        }

        // ============================================
        // 6. 主管 - 获取客服绩效统计
        // ============================================
        [HttpGet("agents/stats")]
        public async Task<IActionResult> GetAgentsStats([FromQuery] Guid? shopId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var userRole = GetCurrentRole();
            Guid targetShopId;
            
            if (userRole == "Supervisor")
            {
                if (!shopId.HasValue) return BadRequest("Supervisor must specify shopId");
                targetShopId = shopId.Value;
            }
            else if (userRole == "Agent")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                targetShopId = agent.ShopId;
            }
            else
            {
                return Forbid();
            }

            var stats = await _statsService.GetAllAgentsPerformanceAsync(targetShopId);
            return Ok(stats);
        }

        // ============================================
        // 7. 主管 - 获取仪表盘统计
        // ============================================
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] Guid? shopId = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var userRole = GetCurrentRole();
            Guid targetShopId;
            
            if (userRole == "Supervisor")
            {
                if (!shopId.HasValue) return BadRequest("Supervisor must specify shopId");
                targetShopId = shopId.Value;
            }
            else if (userRole == "Agent")
            {
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == userId.Value);
                if (agent == null) return NotFound("Agent not found");
                targetShopId = agent.ShopId;
            }
            else
            {
                return Forbid();
            }

            var stats = await _statsService.GetDashboardStatsAsync(targetShopId);
            return Ok(stats);
        }
    }

    public class SupportReplyDto
    {
        public string Content { get; set; } = null!;
    }

    public class ResolveTicketDto
    {
        public int? Satisfaction { get; set; }
    }
}
