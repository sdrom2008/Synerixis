using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Api.Helpers;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 商户端接口（查看会话、转人工、绑定平台）
    /// </summary>
    [Authorize]
    [Route("api/merchant")]
    public class MerchantController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IRepository<ChatSession> _sessionRepo;
        private readonly IMerchantPlatformService _platformService;
        private readonly IPlatformConnectionRepository _connectionRepo;

        public MerchantController(
            AppDbContext db, 
            IRepository<ChatSession> sessionRepo,
            IMerchantPlatformService platformService,
            IPlatformConnectionRepository connectionRepo)
        {
            _db = db;
            _sessionRepo = sessionRepo;
            _platformService = platformService;
            _connectionRepo = connectionRepo;
        }

        /// <summary>
        /// 获取可绑定的平台列表
        /// </summary>
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _platformService.GetAvailablePlatformsAsync();
            return Ok(new { items = platforms });
        }

        /// <summary>
        /// 获取指定平台的授权 URL
        /// </summary>
        [HttpGet("bind/{platform}")]
        public async Task<IActionResult> GetBindUrl(string platform)
        {
            var state = GenerateState();
            var url = await _platformService.GetAuthorizationUrlAsync(platform, state);
            return Ok(new { url, state });
        }

        /// <summary>
        /// 绑定平台店铺（处理 OAuth 回调）
        /// </summary>
        [HttpPost("bind/callback")]
        public async Task<IActionResult> BindCallback([FromBody] BindCallbackRequest request)
        {
            var sellerId = GetCurrentSellerId();
            var result = await _platformService.BindShopAsync(request.Platform, request.Code, sellerId);
            
            if (result.Success)
            {
                return Ok(new { message = "绑定成功", result });
            }
            return BadRequest(new { message = result.Error });
        }

        /// <summary>
        /// 获取已绑定的店铺列表
        /// </summary>
        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            var sellerId = GetCurrentSellerId();
            var connections = await _connectionRepo.GetBySellerIdAndActiveAsync(sellerId);
            
            var items = connections.Select(c => new 
            { 
                c.Id, 
                c.Platform, 
                c.ShopId, 
                c.Nickname, 
                c.AvatarUrl,
                c.IsActive 
            });
            
            return Ok(new { items });
        }

         /// <summary>
        /// 解绑店铺
        /// </summary>
        [HttpPost("unbind/{platform}")]
        public async Task<IActionResult> Unbind(string platform)
        {
            var sellerId = GetCurrentSellerId();
            var connection = await _connectionRepo.GetBySellerIdAsync(sellerId);
            if (connection == null)
                return NotFound("未找到绑定记录");

            await _platformService.UnbindShopAsync(platform, connection.Id);
            return Ok(new { message = "已解绑" });
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

                // 待发草稿 / 未读买家消息优先；其次按买家最近消息时间
                var total = await query.CountAsync();
                var raw = await query
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
                        lastBuyerMessageAt = s.LastBuyerMessageAt,
                        messageCount = s.MessageCount,
                        aiMessageCount = s.AiMessageCount,
                        agentMessageCount = s.AgentMessageCount,
                        hasPendingDraft = _db.DraftMessages.Any(d =>
                            d.ChatSessionId == s.Id && d.Status == DraftStatuses.Pending),
                        unreadBuyerCount = _db.ChatMessages.Count(m =>
                            m.ChatSessionId == s.Id && m.SenderType == 1 && !m.IsRead)
                    })
                    .ToListAsync();

                var now = DateTime.UtcNow;
                var items = raw
                    .Select(s =>
                    {
                        var anchor = s.lastBuyerMessageAt ?? s.lastActiveAt ?? s.createdAt;
                        var hours = Math.Max(0, (now - anchor).TotalHours);
                        return new
                        {
                            s.id,
                            s.sessionId,
                            s.customerName,
                            s.platform,
                            s.status,
                            s.priority,
                            s.assignedAgent,
                            s.assignedAt,
                            s.createdAt,
                            s.lastActiveAt,
                            s.lastBuyerMessageAt,
                            s.messageCount,
                            s.aiMessageCount,
                            s.agentMessageCount,
                            s.hasPendingDraft,
                            s.unreadBuyerCount,
                            hoursSinceLastBuyerMsg = Math.Round(hours, 2),
                            needsResponseBy = anchor.AddHours(12)
                        };
                    })
                    .OrderByDescending(s => s.hasPendingDraft)
                    .ThenByDescending(s => s.unreadBuyerCount > 0)
                    .ThenBy(s => s.needsResponseBy)
                    .ToList();

                var pendingDraftCount = items.Count(i => i.hasPendingDraft);
                return Ok(new { items, total, pendingDraftCount });
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

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id && d.Status == DraftStatuses.Pending)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new { d.Id, d.Content, d.Status, d.CreatedAt, d.UpdatedAt })
                    .FirstOrDefaultAsync();

                var anchor = session.LastBuyerMessageAt ?? session.LastActiveAt ?? session.CreatedAt;
                var now = DateTime.UtcNow;

                return Ok(new
                {
                    sessionStatus = session.Status.ToString(),
                    lastBuyerMessageAt = session.LastBuyerMessageAt,
                    hoursSinceLastBuyerMsg = Math.Round(Math.Max(0, (now - anchor).TotalHours), 2),
                    needsResponseBy = anchor.AddHours(12),
                    pendingDraft = draft,
                    items = messages,
                    // 兼容旧前端：根级也可当数组用时请改读 items
                });
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

                session.TransferToAgent();
                await _db.SaveChangesAsync();

                return Ok(new { message = "已转人工，等待客服接入" });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 商家仪表盘真实 KPI（仅 DB 聚合；不可计算字段返回 null）
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var sellerId = GetCurrentSellerId();
                // ChatSession.ShopId == Seller.Id
                var shopId = sellerId;
                var now = DateTime.UtcNow;
                var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                var sessionsToday = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.CreatedAt >= todayStart);

                var pendingHandoff = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.Status == SessionStatus.Pending);

                var pendingDrafts = await (
                    from d in _db.DraftMessages
                    join s in _db.ChatSessions on d.ChatSessionId equals s.Id
                    where s.ShopId == shopId && d.Status == DraftStatuses.Pending
                    select d.Id
                ).CountAsync();

                var connectedShops = await _db.PlatformConnections
                    .CountAsync(c => c.SellerId == sellerId && c.IsActive);

                // 自动解决率：今日已结束会话中，无人工消息且有 AI 回复的占比；无已结束会话则 null
                var endedToday = await _db.ChatSessions
                    .Where(s => s.ShopId == shopId
                        && s.CreatedAt >= todayStart
                        && (s.Status == SessionStatus.Resolved || s.Status == SessionStatus.Closed))
                    .Select(s => new { s.AiMessageCount, s.AgentMessageCount })
                    .ToListAsync();

                double? autoResolveRate = null;
                if (endedToday.Count > 0)
                {
                    var aiOnly = endedToday.Count(s => s.AgentMessageCount == 0 && s.AiMessageCount > 0);
                    autoResolveRate = Math.Round(aiOnly * 100.0 / endedToday.Count, 1);
                }

                var messagesThisMonth = await (
                    from m in _db.ChatMessages
                    join s in _db.ChatSessions on m.ChatSessionId equals s.Id
                    where s.ShopId == shopId && m.CreatedAt >= monthStart
                    select m.Id
                ).CountAsync();

                return Ok(new
                {
                    sessionsToday,
                    pendingHandoff,
                    pendingDrafts,
                    connectedShops,
                    autoResolveRate,
                    messagesThisMonth,
                    generatedAt = now
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 本月用量摘要（计费页诚实计数）
        /// </summary>
        [HttpGet("usage")]
        public async Task<IActionResult> GetUsage()
        {
            try
            {
                var sellerId = GetCurrentSellerId();
                var shopId = sellerId;
                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                var messagesThisMonth = await (
                    from m in _db.ChatMessages
                    join s in _db.ChatSessions on m.ChatSessionId equals s.Id
                    where s.ShopId == shopId && m.CreatedAt >= monthStart
                    select m.Id
                ).CountAsync();

                var sessionsThisMonth = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.CreatedAt >= monthStart);

                return Ok(new
                {
                    periodStart = monthStart,
                    periodEnd = now,
                    messagesThisMonth,
                    sessionsThisMonth
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        /// <summary>
        /// 列出具有待发送草稿的会话（收件箱「待发送草稿」）
        /// </summary>
        [HttpGet("drafts")]
        public async Task<IActionResult> ListPendingDrafts()
        {
            try
            {
                var shopId = GetCurrentSellerShopId();
                var now = DateTime.UtcNow;
                var rows = await (
                    from d in _db.DraftMessages
                    join s in _db.ChatSessions on d.ChatSessionId equals s.Id
                    where s.ShopId == shopId && d.Status == DraftStatuses.Pending
                    orderby d.CreatedAt descending
                    select new
                    {
                        draftId = d.Id,
                        content = d.Content,
                        createdAt = d.CreatedAt,
                        sessionId = s.Id,
                        sessionBizId = s.SessionId,
                        customerName = s.CustomerName,
                        platform = s.Platform,
                        lastBuyerMessageAt = s.LastBuyerMessageAt,
                        hoursSinceLastBuyerMsg = s.LastBuyerMessageAt.HasValue
                            ? (double?)Math.Round((now - s.LastBuyerMessageAt.Value).TotalHours, 2)
                            : null,
                        needsResponseBy = (s.LastBuyerMessageAt ?? s.LastActiveAt ?? s.CreatedAt).AddHours(12)
                    }
                ).ToListAsync();

                return Ok(new { items = rows, total = rows.Count });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 获取会话当前待发送草稿
        /// </summary>
        [HttpGet("sessions/{id}/draft")]
        public async Task<IActionResult> GetDraft(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id && d.Status == DraftStatuses.Pending)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();

                if (draft == null)
                    return Ok(new { draft = (object?)null, message = "无待发送草稿" });

                return Ok(new
                {
                    draft = new
                    {
                        draft.Id,
                        draft.Content,
                        draft.Status,
                        draft.CreatedAt,
                        draft.UpdatedAt
                    },
                    hoursSinceLastBuyerMsg = Math.Round(session.HoursSinceLastBuyerMessage(), 2),
                    needsResponseBy = session.NeedsResponseBy()
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 审核通过并发送草稿到平台（人审出站）
        /// </summary>
        [HttpPost("sessions/{id}/draft/approve")]
        public async Task<IActionResult> ApproveAndSendDraft(Guid id)
        {
            return await SendDraftInternalAsync(id, editedContent: null);
        }

        /// <summary>
        /// 编辑草稿内容后发送
        /// </summary>
        [HttpPost("sessions/{id}/draft/edit-send")]
        public async Task<IActionResult> EditAndSendDraft(Guid id, [FromBody] DraftEditRequest body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Content))
                return BadRequest(new { message = "内容不能为空" });
            return await SendDraftInternalAsync(id, editedContent: body.Content.Trim());
        }

        /// <summary>
        /// 仅保存编辑后的草稿（不发送）
        /// </summary>
        [HttpPut("sessions/{id}/draft")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] DraftEditRequest body)
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.Content))
                    return BadRequest(new { message = "内容不能为空" });

                var shopId = GetCurrentSellerShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id && d.Status == DraftStatuses.Pending)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();
                if (draft == null)
                    return NotFound(new { message = "无待发送草稿" });

                draft.Content = body.Content.Trim();
                draft.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return Ok(new { message = "草稿已更新", draftId = draft.Id, content = draft.Content });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 丢弃待发送草稿
        /// </summary>
        [HttpPost("sessions/{id}/draft/discard")]
        public async Task<IActionResult> DiscardDraft(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var drafts = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id && d.Status == DraftStatuses.Pending)
                    .ToListAsync();
                if (drafts.Count == 0)
                    return Ok(new { message = "无待发送草稿" });

                foreach (var d in drafts)
                {
                    d.Status = DraftStatuses.Discarded;
                    d.UpdatedAt = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();
                return Ok(new { message = "草稿已丢弃", discarded = drafts.Count });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        private async Task<IActionResult> SendDraftInternalAsync(Guid sessionId, string? editedContent)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();
                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == sessionId && d.Status == DraftStatuses.Pending)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();
                if (draft == null)
                    return NotFound(new { message = "无待发送草稿" });

                if (!string.IsNullOrWhiteSpace(editedContent))
                {
                    draft.Content = editedContent.Trim();
                    draft.UpdatedAt = DateTime.UtcNow;
                }

                if (string.IsNullOrWhiteSpace(session.PlatformConversationId))
                    return BadRequest(new { message = "缺少平台会话 ID，无法出站。请等待买家再次进线或检查 Webhook 解析。" });

                var platformRouter = HttpContext.RequestServices.GetRequiredService<IPlatformClientRouter>();
                var client = platformRouter.GetClient(session.Platform);
                var platformMsg = new PlatformMessage
                {
                    Platform = session.Platform,
                    OpenId = session.PlatformShopOpenId ?? string.Empty,
                    CustomerId = session.CustomerId,
                    CustomerName = session.CustomerName,
                    ConversationId = session.PlatformConversationId,
                    Content = draft.Content
                };

                await client.SendReplyAsync(platformMsg, draft.Content);

                var aiMsg = ChatMessage.FromAI(draft.Content, chatSessionId: session.Id);
                _db.ChatMessages.Add(aiMsg);
                session.AddAiMessage();

                draft.Status = DraftStatuses.Sent;
                draft.SentAt = DateTime.UtcNow;
                draft.SentMessageId = aiMsg.Id;
                draft.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "已发送到平台",
                    draftId = draft.Id,
                    messageId = aiMsg.Id
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


                private string GenerateState()
        {
            var buffer = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer);
            return Convert.ToBase64String(buffer);
        }
    }

    public class BindCallbackRequest
    {
        public string Platform { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class DraftEditRequest
    {
        public string Content { get; set; } = string.Empty;
    }
}