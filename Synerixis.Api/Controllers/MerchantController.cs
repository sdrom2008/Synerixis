using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Api.Helpers;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 商户端接口（查看会话、转人工）
    /// </summary>
    [Authorize]
    [Route("api/merchant")]
    public class MerchantController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IRepository<ChatSession> _sessionRepo;

        public MerchantController(AppDbContext db, IRepository<ChatSession> sessionRepo)
        {
            _db = db;
            _sessionRepo = sessionRepo;
        }

        /// <summary>
        /// 获取本店的所有会话列表
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] string? status = null)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var query = _db.ChatSessions
                    .Include(s => s.AssignedAgent)
                    .Where(s => s.ShopId == shopId);

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<SessionStatus>(status, true, out var sessionStatus))
                    {
                        query = query.Where(s => s.Status == sessionStatus);
                    }
                    else
                    {
                        return BadRequest(new { message = "无效的状态参数" });
                    }
                }

                query = query
                    .OrderByDescending(s => s.CreatedAt);

                var total = await query.CountAsync();
                var items = await query
                    .Select(s => new
                    {
                        id = s.Id,
                        sessionId = s.SessionId,
                        customerName = s.CustomerName,
                        platform = s.Platform,
                        status = ((SessionStatus)s.Status).ToString(),
                        priority = ((SessionPriority)s.Priority).ToString(),
                        assignedAgent = s.AssignedAgent != null ? new
                        {
                            s.AssignedAgent.Id,
                            s.AssignedAgent.Name
                        } : null,
                        assignedAt = s.AssignedAt,
                        createdAt = s.CreatedAt,
                        lastActiveAt = s.LastActiveAt,
                        messageCount = s.MessageCount,
                        aiMessageCount = s.AiMessageCount,
                        agentMessageCount = s.AgentMessageCount
                    })
                    .ToListAsync();

                return Ok(new { items, total });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 获取指定会话的详细消息（只读）
        /// </summary>
        [HttpGet("sessions/{id}/messages")]
        public async Task<IActionResult> GetSessionMessages(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var messages = await _db.ChatMessages
                    .Where(m => m.ChatSessionId == id)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new
                    {
                        id = m.Id,
                        content = m.Content,
                        senderType = m.SenderType == 1 ? "Customer" : (m.SenderType == 2 ? "Agent" : "System"),
                        messageType = m.MessageType == 1 ? "text" : "other",
                        metadata = m.Metadata != null ? m.Metadata : null,
                        createdAt = m.CreatedAt
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 商户手动将会话转人工（标记为 Pending 并解除分配）
        /// </summary>
        [HttpPost("sessions/{id}/transfer")]
        public async Task<IActionResult> TransferToAgent(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                // 只有非 Closed 的会话才能转人工
                if (session.Status == SessionStatus.Closed)
                    return BadRequest(new { message = "已关闭的会话不能转人工" });

                // 重置为待分配状态
                session.AssignedAgentId = null;
                session.Status = SessionStatus.Pending;
                session.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Ok(new { message = "已转人工，等待客服接入" });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
