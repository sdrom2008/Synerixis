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
        /// 收件箱多店筛选选项（Seller / 坐席均可，按本店 shopId）。
        /// </summary>
        [HttpGet("shop-options")]
        public async Task<IActionResult> GetShopOptions()
        {
            try
            {
                var shopId = GetMerchantShopId();
                var connections = await _connectionRepo.GetBySellerIdAndActiveAsync(shopId);
                var items = connections.Select(c => new
                {
                    connectionId = c.Id,
                    platform = c.Platform,
                    shopId = c.ShopId,
                    nickname = c.Nickname ?? c.ShopId ?? c.Platform
                });
                return Ok(new { items });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 获取指定平台的授权 URL
        /// </summary>
        [HttpGet("bind/{platform}")]
        public async Task<IActionResult> GetBindUrl(string platform)
        {
            if (!CanManageShopOwnerResources())
                return Forbid();
            try
            {
                _ = GetShopOwnerSellerId();
                var state = GenerateState();
                var url = await _platformService.GetAuthorizationUrlAsync(platform, state);
                return Ok(new { url, state });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 绑定平台店铺（处理 OAuth 回调）。Seller / Supervisor / Admin；Agent 403。
        /// </summary>
        [HttpPost("bind/callback")]
        public async Task<IActionResult> BindCallback([FromBody] BindCallbackRequest request)
        {
            if (!CanManageShopOwnerResources())
                return Forbid();
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var result = await _platformService.BindShopAsync(request.Platform, request.Code, sellerId);

                if (result.Success)
                    return Ok(new { message = "绑定成功", result });
                return BadRequest(new { message = result.Error });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 获取已绑定的店铺列表。Seller / Supervisor / Admin；Agent 403。
        /// </summary>
        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            if (!CanManageShopOwnerResources())
                return Forbid();
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var connections = await _connectionRepo.GetBySellerIdAndActiveAsync(sellerId);
                var now = DateTime.UtcNow;

                var items = connections.Select(c =>
                {
                    string tokenStatus;
                    if (!c.IsActive) tokenStatus = "inactive";
                    else if (c.TokenExpiresAt.HasValue)
                    {
                        if (c.TokenExpiresAt.Value <= now) tokenStatus = "expired";
                        else if (c.TokenExpiresAt.Value <= now.AddHours(1)) tokenStatus = "expiring";
                        else tokenStatus = "valid";
                    }
                    else
                    {
                        var anchor = c.UpdatedAt ?? c.CreatedAt;
                        if (anchor <= now.AddHours(-4)) tokenStatus = "expiring";
                        else tokenStatus = "valid";
                    }

                    return new
                    {
                        c.Id,
                        c.Platform,
                        shopId = c.ShopId,
                        nickname = c.Nickname,
                        c.AvatarUrl,
                        c.IsActive,
                        tokenExpiresAt = c.TokenExpiresAt,
                        tokenStatus,
                        tokenHint = tokenStatus switch
                        {
                            "expired" => "Token 已过期，请刷新或重新绑定",
                            "expiring" => "Token 即将过期",
                            "inactive" => "已停用",
                            _ => "有效"
                        },
                        updatedAt = c.UpdatedAt
                    };
                });

                return Ok(new { items });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 手动刷新指定连接的 access token。
        /// </summary>
        [HttpPost("connections/{id:guid}/refresh")]
        public async Task<IActionResult> RefreshConnection(Guid id)
        {
            if (!CanManageShopOwnerResources())
                return Forbid();
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var connection = await _connectionRepo.GetByIdAsync(id);
                if (connection == null || connection.SellerId != sellerId)
                    return NotFound(new { message = "未找到绑定记录" });

                var ok = await _platformService.RefreshTokenAsync(connection.Platform, connection);
                if (!ok)
                    return BadRequest(new { message = "刷新失败（可能缺少 RefreshToken 或平台拒绝）" });

                return Ok(new
                {
                    message = "Token 已刷新",
                    connectionId = connection.Id,
                    tokenExpiresAt = connection.TokenExpiresAt,
                    updatedAt = connection.UpdatedAt
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 解绑店铺。Seller / Supervisor / Admin；Agent 403。
        /// </summary>
        [HttpPost("unbind/{platform}")]
        public async Task<IActionResult> Unbind(string platform)
        {
            if (!CanManageShopOwnerResources())
                return Forbid();
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var connections = await _connectionRepo.GetBySellerIdAndActiveAsync(sellerId);
                var normalized = (platform ?? string.Empty).ToUpperInvariant();
                var connection = connections.FirstOrDefault(c =>
                    string.Equals(c.Platform, normalized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Platform, platform, StringComparison.OrdinalIgnoreCase));
                if (connection == null)
                    return NotFound(new { message = "未找到绑定记录" });

                await _platformService.UnbindShopAsync(connection.Platform, connection.Id);
                return Ok(new { message = "已解绑" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 获取本店的所有会话列表。支持 platform / connectionId / platformShopId 多店筛选。
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(
            [FromQuery] string? status = null,
            [FromQuery] string? platform = null,
            [FromQuery] Guid? connectionId = null,
            [FromQuery] string? platformShopId = null)
        {
            try
            {
                var shopId = GetMerchantShopId();

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

                string? filterPlatform = string.IsNullOrWhiteSpace(platform)
                    ? null
                    : platform.Trim().ToUpperInvariant();
                string? filterShopOpenId = string.IsNullOrWhiteSpace(platformShopId)
                    ? null
                    : platformShopId.Trim();

                if (connectionId.HasValue)
                {
                    var conn = await _db.PlatformConnections.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == connectionId.Value && c.SellerId == shopId);
                    if (conn == null)
                        return BadRequest(new { message = "无效的 connectionId" });
                    filterPlatform = (conn.Platform ?? string.Empty).ToUpperInvariant();
                    filterShopOpenId = !string.IsNullOrEmpty(conn.OpenId)
                        ? conn.OpenId
                        : conn.ShopId;
                }

                if (!string.IsNullOrEmpty(filterPlatform))
                {
                    var fp = filterPlatform;
                    // 兼容历史大小写；避免依赖 SQL ToUpper 翻译
                    query = query.Where(s =>
                        s.Platform == fp
                        || s.Platform == filterPlatform
                        || (s.Platform != null && s.Platform.ToLower() == fp.ToLower()));
                }

                if (!string.IsNullOrEmpty(filterShopOpenId))
                {
                    var oid = filterShopOpenId;
                    query = query.Where(s =>
                        s.PlatformShopOpenId == oid
                        || (s.PlatformShopOpenId == null || s.PlatformShopOpenId == ""));
                    // Prefer exact openId match when present; include legacy null for demo
                    // Re-filter in memory below for precision when mixed.
                }

                var sellerConfig = await _db.SellerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == shopId);
                var slaHours = sellerConfig?.ResponseSlaHours > 0 ? sellerConfig.ResponseSlaHours : 12;

                var connections = await _db.PlatformConnections.AsNoTracking()
                    .Where(c => c.SellerId == shopId && c.IsActive)
                    .Select(c => new { c.Id, c.Platform, c.ShopId, c.OpenId, c.Nickname })
                    .ToListAsync();

                var total = await query.CountAsync();
                var raw = await query
                    .Select(s => new
                    {
                        id = s.Id,
                        sessionId = s.SessionId,
                        customerName = s.CustomerName,
                        platform = s.Platform,
                        platformShopOpenId = s.PlatformShopOpenId,
                        status = ((SessionStatus)s.Status).ToString(),
                        priority = ((SessionPriority)s.Priority).ToString(),
                        pendingHumanHandoff = s.PendingHumanHandoff,
                        handoffAt = s.HandoffAt,
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
                            d.ChatSessionId == s.Id
                            && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded)),
                        unreadBuyerCount = _db.ChatMessages.Count(m =>
                            m.ChatSessionId == s.Id && m.SenderType == 1 && !m.IsRead)
                    })
                    .ToListAsync();

                // 精确店铺：有 PlatformShopOpenId 时必须匹配；无则仅当未指定店铺筛或同平台唯一连接
                if (!string.IsNullOrEmpty(filterShopOpenId))
                {
                    var oid = filterShopOpenId;
                    raw = raw.Where(s =>
                        string.IsNullOrEmpty(s.platformShopOpenId)
                        || string.Equals(s.platformShopOpenId, oid, StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    total = raw.Count;
                }

                var now = DateTime.UtcNow;
                var items = raw
                    .Select(s =>
                    {
                        var anchor = s.lastBuyerMessageAt ?? s.lastActiveAt ?? s.createdAt;
                        var hours = Math.Max(0, (now - anchor).TotalHours);
                        var needsBy = anchor.AddHours(slaHours);
                        string slaUrgency;
                        if (now >= needsBy) slaUrgency = "overdue";
                        else if ((needsBy - now).TotalHours <= Math.Max(0.5, slaHours * 0.25)) slaUrgency = "soon";
                        else slaUrgency = "ok";

                        var plat = (s.platform ?? "").ToUpperInvariant();
                        var match = connections.FirstOrDefault(c =>
                            string.Equals(c.Platform, plat, StringComparison.OrdinalIgnoreCase)
                            && (
                                (!string.IsNullOrEmpty(s.platformShopOpenId)
                                    && (c.OpenId == s.platformShopOpenId || c.ShopId == s.platformShopOpenId))
                                || string.IsNullOrEmpty(s.platformShopOpenId)
                            ));
                        if (match == null)
                        {
                            match = connections.FirstOrDefault(c =>
                                string.Equals(c.Platform, plat, StringComparison.OrdinalIgnoreCase));
                        }

                        return new
                        {
                            s.id,
                            s.sessionId,
                            s.customerName,
                            s.platform,
                            platformShopOpenId = s.platformShopOpenId,
                            shopNickname = match?.Nickname,
                            connectionId = match?.Id,
                            s.status,
                            s.priority,
                            s.pendingHumanHandoff,
                            s.handoffAt,
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
                            responseSlaHours = slaHours,
                            hoursSinceLastBuyerMsg = Math.Round(hours, 2),
                            needsResponseBy = needsBy,
                            slaUrgency
                        };
                    })
                    .OrderByDescending(s => s.hasPendingDraft)
                    .ThenByDescending(s => s.unreadBuyerCount > 0)
                    .ThenBy(s => s.needsResponseBy)
                    .ToList();

                var pendingDraftCount = items.Count(i => i.hasPendingDraft);
                return Ok(new
                {
                    items,
                    total,
                    pendingDraftCount,
                    responseSlaHours = slaHours,
                    filters = new { platform = filterPlatform, connectionId, platformShopId = filterShopOpenId }
                });
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
                var shopId = GetMerchantShopId();

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
                    .Where(d => d.ChatSessionId == id
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded))
                    .OrderByDescending(d => d.Status == DraftStatuses.Pending)
                    .ThenByDescending(d => d.CreatedAt)
                    .Select(d => new { d.Id, d.Content, d.Status, d.CreatedAt, d.UpdatedAt })
                    .FirstOrDefaultAsync();

                var sellerConfig = await _db.SellerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == shopId);
                var slaHours = sellerConfig?.ResponseSlaHours > 0 ? sellerConfig.ResponseSlaHours : 12;
                var anchor = session.LastBuyerMessageAt ?? session.LastActiveAt ?? session.CreatedAt;
                var now = DateTime.UtcNow;
                var needsBy = anchor.AddHours(slaHours);
                string slaUrgency;
                if (now >= needsBy) slaUrgency = "overdue";
                else if ((needsBy - now).TotalHours <= Math.Max(0.5, slaHours * 0.25)) slaUrgency = "soon";
                else slaUrgency = "ok";

                return Ok(new
                {
                    sessionStatus = session.Status.ToString(),
                    pendingHumanHandoff = session.PendingHumanHandoff,
                    handoffAt = session.HandoffAt,
                    lastBuyerMessageAt = session.LastBuyerMessageAt,
                    responseSlaHours = slaHours,
                    hoursSinceLastBuyerMsg = Math.Round(Math.Max(0, (now - anchor).TotalHours), 2),
                    needsResponseBy = needsBy,
                    slaUrgency,
                    pendingDraft = draft,
                    items = messages,
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 会话关联订单（本地 Orders 优先；失败返回空列表不炸）。
        /// </summary>
        [HttpGet("sessions/{id:guid}/orders")]
        public async Task<IActionResult> GetSessionOrders(Guid id, [FromQuery] int limit = 10)
        {
            try
            {
                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var take = Math.Clamp(limit, 1, 50);
                var orders = await _db.Orders
                    .AsNoTracking()
                    .Where(o => o.ShopId == shopId && o.CustomerId == session.CustomerId)
                    .OrderByDescending(o => o.OrderTime)
                    .Take(take)
                    .Select(o => new
                    {
                        id = o.Id,
                        orderNo = o.OrderNo,
                        status = o.Status,
                        totalAmount = o.TotalAmount,
                        paymentAmount = o.PaymentAmount,
                        platform = o.Platform ?? session.Platform,
                        orderTime = o.OrderTime,
                        paidAt = o.PaidAt,
                        shippedAt = o.ShippedAt,
                        logisticsNo = o.LogisticsNo,
                        logisticsCompany = o.LogisticsCompany
                    })
                    .ToListAsync();

                // 本地为空时尝试平台客户端（若已有能力）；失败吞掉返回空
                if (orders.Count == 0)
                {
                    try
                    {
                        var router = HttpContext.RequestServices.GetService<IPlatformClientRouter>();
                        if (router != null && !string.IsNullOrEmpty(session.Platform)
                            && !string.IsNullOrEmpty(session.CustomerId))
                        {
                            var client = router.GetClient(session.Platform);
                            // 平台查单能力因客户端而异；无接口则跳过
                            _ = client;
                        }
                    }
                    catch
                    {
                        // ignore platform lookup failures
                    }
                }

                return Ok(new
                {
                    sessionId = session.Id,
                    customerId = session.CustomerId,
                    items = orders,
                    total = orders.Count,
                    empty = orders.Count == 0
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
                var shopId = GetMerchantShopId();

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                // 只有非 Closed 的会话才能转人工
                if (session.Status == SessionStatus.Closed)
                    return BadRequest(new { message = "已关闭的会话不能转人工" });

                session.TransferToAgent();

                // 可选：旧 Pending 草稿标记 Superseded（仍可人工发送，停止被当作「最新 AI 待审」）
                var pendingDrafts = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == session.Id && d.Status == DraftStatuses.Pending)
                    .ToListAsync();
                foreach (var d in pendingDrafts)
                {
                    d.Status = DraftStatuses.Superseded;
                    d.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "已转人工：已停止 AI 新草稿与 AutoSend，旧草稿仍可手动发送",
                    pendingHumanHandoff = true,
                    supersededDrafts = pendingDrafts.Count
                });
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
                var shopId = GetMerchantShopId();
                // ChatSession.ShopId == Seller.Id；店铺连接也按 SellerId=shopId
                var sellerId = shopId;
                var now = DateTime.UtcNow;
                var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                var sessionsToday = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.CreatedAt >= todayStart);

                var pendingHandoff = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.PendingHumanHandoff);

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
                var shopId = GetMerchantShopId();
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
        /// SLA 超时唤醒告警列表。阈值默认读 SellerConfig.AlertThresholdHours（如 1,3,12）。
        /// Push/声音 TODO；本接口供收件箱角标与简单告警面板使用。
        /// </summary>
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts([FromQuery] string? thresholds = null)
        {
            try
            {
                var shopId = GetMerchantShopId();
                var config = await _db.SellerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == shopId);
                var slaHours = config?.ResponseSlaHours > 0 ? config.ResponseSlaHours : 12;
                var rawThresholds = !string.IsNullOrWhiteSpace(thresholds)
                    ? thresholds
                    : (config?.AlertThresholdHours ?? "1,3,12");
                var hoursList = rawThresholds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => double.TryParse(s, out var h) ? h : (double?)null)
                    .Where(h => h.HasValue && h.Value > 0)
                    .Select(h => h!.Value)
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();
                if (hoursList.Count == 0)
                    hoursList = new List<double> { 1, 3, 12 };

                var now = DateTime.UtcNow;
                var openStatuses = new[] { SessionStatus.Pending, SessionStatus.Active };
                var sessions = await _db.ChatSessions
                    .AsNoTracking()
                    .Where(s => s.ShopId == shopId && openStatuses.Contains(s.Status))
                    .Select(s => new
                    {
                        s.Id,
                        s.SessionId,
                        s.CustomerName,
                        s.Platform,
                        status = s.Status.ToString(),
                        s.PendingHumanHandoff,
                        s.LastBuyerMessageAt,
                        s.LastActiveAt,
                        s.CreatedAt
                    })
                    .ToListAsync();

                var alertItems = sessions
                    .Select(s =>
                    {
                        var msgAnchor = s.LastBuyerMessageAt ?? s.LastActiveAt ?? s.CreatedAt;
                        var hours = Math.Max(0, (now - msgAnchor).TotalHours);
                        var crossed = hoursList.Where(t => hours >= t).ToList();
                        if (crossed.Count == 0) return null;
                        var highest = crossed.Max();
                        var needsBy = msgAnchor.AddHours(slaHours);
                        return new
                        {
                            sessionId = s.Id,
                            sessionBizId = s.SessionId,
                            customerName = s.CustomerName,
                            platform = s.Platform,
                            status = s.status,
                            pendingHumanHandoff = s.PendingHumanHandoff,
                            hoursSinceLastBuyerMsg = Math.Round(hours, 2),
                            thresholdHours = highest,
                            needsResponseBy = needsBy,
                            slaUrgency = now >= needsBy ? "overdue" : "soon",
                            // TODO: push / sound notification channel
                            channel = "in-app"
                        };
                    })
                    .Where(a => a != null)
                    .OrderByDescending(a => a!.hoursSinceLastBuyerMsg)
                    .ToList();

                return Ok(new
                {
                    items = alertItems,
                    total = alertItems.Count,
                    responseSlaHours = slaHours,
                    thresholds = hoursList,
                    // TODO: WebPush / 桌面声音
                    pushStub = true
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
                var shopId = GetMerchantShopId();
                var now = DateTime.UtcNow;
                var rows = await (
                    from d in _db.DraftMessages
                    join s in _db.ChatSessions on d.ChatSessionId equals s.Id
                    where s.ShopId == shopId
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded)
                    orderby d.CreatedAt descending
                    select new
                    {
                        draftId = d.Id,
                        content = d.Content,
                        status = d.Status,
                        createdAt = d.CreatedAt,
                        sessionId = s.Id,
                        sessionBizId = s.SessionId,
                        customerName = s.CustomerName,
                        platform = s.Platform,
                        pendingHumanHandoff = s.PendingHumanHandoff,
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
                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded))
                    .OrderByDescending(d => d.Status == DraftStatuses.Pending)
                    .ThenByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync();

                if (draft == null)
                    return Ok(new { draft = (object?)null, message = "无待发送草稿" });

                var cfg = await _db.SellerConfigs.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == shopId);
                var sla = cfg?.ResponseSlaHours > 0 ? cfg.ResponseSlaHours : 12;

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
                    pendingHumanHandoff = session.PendingHumanHandoff,
                    hoursSinceLastBuyerMsg = Math.Round(session.HoursSinceLastBuyerMessage(), 2),
                    needsResponseBy = session.NeedsResponseBy(sla),
                    responseSlaHours = sla
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

                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded))
                    .OrderByDescending(d => d.Status == DraftStatuses.Pending)
                    .ThenByDescending(d => d.CreatedAt)
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
                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var drafts = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == id
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded))
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
                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var draft = await _db.DraftMessages
                    .Where(d => d.ChatSessionId == sessionId
                        && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded))
                    .OrderByDescending(d => d.Status == DraftStatuses.Pending)
                    .ThenByDescending(d => d.CreatedAt)
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