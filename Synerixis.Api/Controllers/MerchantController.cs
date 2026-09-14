using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Api.Helpers;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Repositories;
using Synerixis.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly IOAuthBindStateStore _oauthStateStore;
        private readonly IConfiguration _config;
        private readonly IPlatformClientRouter _platformRouter;
        private readonly ILogger<MerchantController> _logger;
        private readonly IAuditLogger _audit;

        public MerchantController(
            AppDbContext db, 
            IRepository<ChatSession> sessionRepo,
            IMerchantPlatformService platformService,
            IPlatformConnectionRepository connectionRepo,
            IOAuthBindStateStore oauthStateStore,
            IConfiguration config,
            IPlatformClientRouter platformRouter,
            ILogger<MerchantController> logger,
            IAuditLogger audit)
        {
            _db = db;
            _sessionRepo = sessionRepo;
            _platformService = platformService;
            _connectionRepo = connectionRepo;
            _oauthStateStore = oauthStateStore;
            _config = config;
            _platformRouter = platformRouter;
            _logger = logger;
            _audit = audit;
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
                var sellerId = GetShopOwnerSellerId();
                var state = GenerateState();
                _oauthStateStore.Put(state, sellerId, platform);
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
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.ConnectionBind,
                        "PlatformConnection", result.ConnectionId.ToString(),
                        new { platform = request.Platform },
                        sellerId);
                    return Ok(new { message = "绑定成功", result });
                }
                return BadRequest(new { message = result.Error });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 平台 OAuth RedirectUri 落地（匿名）：收 code/state/platform，尽量完成 BindShop，再跳转 merchant-web /shops?bound=1。
        /// 开发可用 query platform；state 命中内存短存则带 sellerId。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("bind/callback")]
        [HttpGet("/api/oauth/{platformRoute}/callback")]
        public async Task<IActionResult> OAuthBindRedirectCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? platform,
            [FromRoute] string? platformRoute = null)
        {
            var frontendBase = (_config["Frontends:MerchantWebBaseUrl"]
                ?? _config["MerchantWeb:BaseUrl"]
                ?? "http://localhost:5173").TrimEnd('/');
            var successUrl = $"{frontendBase}/shops?bound=1";
            var failUrl = $"{frontendBase}/shops?bound=0&error=";

            var plat = !string.IsNullOrWhiteSpace(platform)
                ? platform!
                : (platformRoute ?? string.Empty);
            plat = plat.Trim();

            Guid sellerId = default;
            string statePlatform = string.Empty;
            var hasState = !string.IsNullOrWhiteSpace(state)
                && _oauthStateStore.TryTake(state!, out sellerId, out statePlatform);
            if (hasState && string.IsNullOrWhiteSpace(plat))
                plat = statePlatform;

            if (string.IsNullOrWhiteSpace(code))
                return Redirect(failUrl + Uri.EscapeDataString("missing_code"));
            if (string.IsNullOrWhiteSpace(plat))
                return Redirect(failUrl + Uri.EscapeDataString("missing_platform"));

            if (!hasState)
            {
                // 开发兜底：无 state 时无法知 seller；拒绝以免绑错店
                _logger.LogWarning("[OAuth] GET callback missing/expired state; platform={Platform}", plat);
                return Redirect(failUrl + Uri.EscapeDataString("invalid_or_expired_state"));
            }

            try
            {
                var result = await _platformService.BindShopAsync(plat, code!, sellerId);
                if (result.Success)
                {
                    await _audit.LogAsync(sellerId, "Seller", AuditActions.ConnectionBind,
                        "PlatformConnection", result.ConnectionId.ToString(),
                        new { platform = plat, via = "oauth_redirect" },
                        sellerId);
                    return Redirect(successUrl);
                }
                return Redirect(failUrl + Uri.EscapeDataString(result.Error ?? "bind_failed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OAuth] GET callback BindShop failed");
                return Redirect(failUrl + Uri.EscapeDataString("server_error"));
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
                    // status: ok | expiring | expired | unknown（24h 内过期视为 expiring）
                    string status;
                    double? expiresInHours = null;
                    if (!c.IsActive)
                        status = "unknown";
                    else if (c.TokenExpiresAt.HasValue)
                    {
                        expiresInHours = Math.Round((c.TokenExpiresAt.Value - now).TotalHours, 2);
                        if (c.TokenExpiresAt.Value <= now) status = "expired";
                        else if (c.TokenExpiresAt.Value <= now.AddHours(24)) status = "expiring";
                        else status = "ok";
                    }
                    else
                        status = "unknown";

                    // 兼容旧字段名
                    var tokenStatus = status switch
                    {
                        "ok" => "valid",
                        "unknown" when !c.IsActive => "inactive",
                        _ => status
                    };

                    var needsRebind = status == "expired" && !string.IsNullOrEmpty(c.LastRefreshError);
                    return new
                    {
                        c.Id,
                        c.Platform,
                        shopId = c.ShopId,
                        nickname = c.Nickname,
                        c.AvatarUrl,
                        c.IsActive,
                        expiresAt = c.TokenExpiresAt,
                        expiresInHours,
                        status,
                        tokenExpiresAt = c.TokenExpiresAt,
                        tokenStatus,
                        lastRefreshError = c.LastRefreshError,
                        lastRefreshAt = c.LastRefreshAt,
                        needsRebind,
                        tokenHint = needsRebind
                            ? "需重新授权"
                            : status switch
                        {
                            "expired" => "Token 已过期，请立即刷新或重新绑定",
                            "expiring" => "Token 将在 24 小时内过期",
                            "unknown" => c.IsActive ? "过期时间未知" : "已停用",
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
                {
                    // 重新加载以拿到 LastRefreshError（RefreshTokenAsync 已落库）
                    connection = await _connectionRepo.GetByIdAsync(id) ?? connection;
                    var errMsg = connection.LastRefreshError
                        ?? "刷新失败（可能缺少 RefreshToken 或平台拒绝），请重新授权绑定";
                    var actorFail = GetCurrentUser();
                    await _audit.LogAsync(actorFail.UserId, actorFail.UserType, AuditActions.ConnectionRefresh,
                        "PlatformConnection", connection.Id.ToString(),
                        new { platform = connection.Platform, shopId = connection.ShopId, success = false, error = errMsg },
                        sellerId);
                    return BadRequest(new
                    {
                        errorCode = "TOKEN_REFRESH_FAILED",
                        rebindRequired = true,
                        message = errMsg,
                        connectionId = connection.Id,
                        lastRefreshError = connection.LastRefreshError,
                        lastRefreshAt = connection.LastRefreshAt,
                        // 兼容别名
                        code = "REBIND_REQUIRED"
                    });
                }

                var actor = GetCurrentUser();
                await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.ConnectionRefresh,
                    "PlatformConnection", connection.Id.ToString(),
                    new { platform = connection.Platform, shopId = connection.ShopId, tokenExpiresAt = connection.TokenExpiresAt, success = true },
                    sellerId);

                return Ok(new
                {
                    message = "Token 已刷新",
                    connectionId = connection.Id,
                    expiresAt = connection.TokenExpiresAt,
                    tokenExpiresAt = connection.TokenExpiresAt,
                    lastRefreshAt = connection.LastRefreshAt,
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
                var actor = GetCurrentUser();
                await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.ConnectionUnbind,
                    "PlatformConnection", connection.Id.ToString(),
                    new { platform = connection.Platform, shopId = connection.ShopId },
                    sellerId);
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
            [FromQuery] string? platformShopId = null,
            [FromQuery] string? assignment = null)
        {
            try
            {
                var shopId = GetMerchantShopId();

                var query = _db.ChatSessions
                    .Include(s => s.AssignedAgent)
                    .Where(s => s.ShopId == shopId);

                // assignment: unassigned | mine（mine 仅坐席有效）
                if (!string.IsNullOrWhiteSpace(assignment))
                {
                    var a = assignment.Trim().ToLowerInvariant();
                    if (a == "unassigned")
                    {
                        query = query.Where(s => s.AssignedAgentId == null);
                    }
                    else if (a == "mine")
                    {
                        var current = GetCurrentUser();
                        if (current.IsStaff)
                            query = query.Where(s => s.AssignedAgentId == current.UserId);
                        else
                            query = query.Where(s => false); // Seller 无「分给我」
                    }
                }

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
                    filters = new { platform = filterPlatform, connectionId, platformShopId = filterShopOpenId, assignment }
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
                    .Include(s => s.AssignedAgent)
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
                    assignedAgent = session.AssignedAgent != null ? new
                    {
                        session.AssignedAgent.Id,
                        session.AssignedAgent.Name,
                        role = session.AssignedAgent.Role.ToString()
                    } : null,
                    assignedAt = session.AssignedAt,
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
        /// 会话关联订单：本地 Orders 优先；空则用会话 platform + customerId + PlatformShopOpenId 回源平台查单。
        /// 平台失败返回 items=[] + warning，不 500。
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
                var local = await _db.Orders
                    .AsNoTracking()
                    .Where(o => o.ShopId == shopId && o.CustomerId == session.CustomerId)
                    .OrderByDescending(o => o.OrderTime)
                    .Take(take)
                    .Select(o => new
                    {
                        id = (Guid?)o.Id,
                        orderNo = o.OrderNo,
                        status = o.Status,
                        totalAmount = (decimal?)o.TotalAmount,
                        paymentAmount = (decimal?)o.PaymentAmount,
                        platform = o.Platform ?? session.Platform,
                        orderTime = (DateTime?)o.OrderTime,
                        paidAt = o.PaidAt,
                        shippedAt = o.ShippedAt,
                        logisticsNo = o.LogisticsNo,
                        logisticsCompany = o.LogisticsCompany,
                        source = "local",
                        summary = (string?)null
                    })
                    .ToListAsync();

                string? warning = null;
                object items = local;
                var source = "local";

                if (local.Count == 0)
                {
                    source = "platform";
                    try
                    {
                        if (!string.IsNullOrEmpty(session.Platform)
                            && !string.IsNullOrEmpty(session.CustomerId)
                            && _platformRouter.IsSupported(session.Platform))
                        {
                            var client = _platformRouter.GetClient(session.Platform);
                            var summary = await client.GetCustomerOrderAsync(
                                session.Platform,
                                session.CustomerId!,
                                session.PlatformShopOpenId);

                            if (!string.IsNullOrWhiteSpace(summary))
                            {
                                var parsed = ParsePlatformOrderSummary(summary!, session.Platform);
                                items = new[] { parsed };
                            }
                            else
                            {
                                items = Array.Empty<object>();
                                warning = "platform_empty";
                            }
                        }
                        else
                        {
                            items = Array.Empty<object>();
                            warning = "platform_unsupported_or_missing_customer";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Orders] Platform lookup failed for session {SessionId}", id);
                        items = Array.Empty<object>();
                        warning = "platform_lookup_failed";
                    }
                }

                var itemList = items as System.Collections.ICollection;
                var total = itemList?.Count ?? 0;

                return Ok(new
                {
                    sessionId = session.Id,
                    customerId = session.CustomerId,
                    source,
                    warning,
                    items,
                    total,
                    empty = total == 0
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        private static object ParsePlatformOrderSummary(string summary, string? platform)
        {
            // FormatOrderSummary: "order_sn=X, status=Y" or "... tracking=Z"
            string? orderNo = null, status = null, tracking = null;
            foreach (var part in summary.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (kv.Length != 2) continue;
                if (kv[0].Equals("order_sn", StringComparison.OrdinalIgnoreCase)) orderNo = kv[1];
                else if (kv[0].Equals("status", StringComparison.OrdinalIgnoreCase)) status = kv[1];
                else if (kv[0].Equals("tracking", StringComparison.OrdinalIgnoreCase)) tracking = kv[1];
            }
            return new
            {
                id = (Guid?)null,
                orderNo = orderNo ?? summary,
                status = status ?? "unknown",
                totalAmount = (decimal?)null,
                paymentAmount = (decimal?)null,
                platform,
                orderTime = (DateTime?)null,
                paidAt = (DateTime?)null,
                shippedAt = (DateTime?)null,
                logisticsNo = tracking,
                logisticsCompany = (string?)null,
                source = "platform",
                summary
            };
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

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.SessionHandoff,
                        "ChatSession", session.Id.ToString(),
                        new { supersededDrafts = pendingDrafts.Count },
                        shopId);
                }
                catch { /* ignore audit actor errors */ }

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
        /// 本店坐席简表（收件箱分配下拉）：id / name / role / online
        /// </summary>
        [HttpGet("agents")]
        public async Task<IActionResult> GetShopAgents()
        {
            try
            {
                var shopId = GetMerchantShopId();
                var items = await _db.Agents
                    .AsNoTracking()
                    .Where(a => a.ShopId == shopId && a.IsActive)
                    .OrderBy(a => a.Role)
                    .ThenBy(a => a.Name)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        role = a.Role.ToString(),
                        online = a.IsOnline
                    })
                    .ToListAsync();
                return Ok(new { items, total = items.Count });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 商家工作台：将会话分配给本店坐席（Seller / Supervisor；Admin 同店亦可）。
        /// 与 TransferToAgent（handoff 闸）可并存：转人工后仍可指定人。
        /// </summary>
        [HttpPost("sessions/{id:guid}/assign")]
        public async Task<IActionResult> AssignSession(Guid id, [FromBody] MerchantAssignDto dto)
        {
            try
            {
                var current = GetCurrentUser();
                if (!current.IsSeller && !current.IsSupervisor && !current.IsAdmin)
                    return Forbid();

                var shopId = GetMerchantShopId();
                if (dto == null || dto.AgentId == Guid.Empty)
                    return BadRequest(new { message = "请指定 agentId" });

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });
                if (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Resolved)
                    return BadRequest(new { message = "已结束的会话不能分配" });

                var agent = await _db.Agents
                    .FirstOrDefaultAsync(a => a.Id == dto.AgentId && a.ShopId == shopId && a.IsActive);
                if (agent == null)
                    return BadRequest(new { message = "坐席不存在或不属于本店" });

                session.AssignToAgent(agent.Id);
                await _db.SaveChangesAsync();

                try
                {
                    await _audit.LogAsync(current.UserId, current.UserType, AuditActions.SessionAssign,
                        "ChatSession", session.Id.ToString(),
                        new { agentId = agent.Id, agentName = agent.Name, pendingHumanHandoff = session.PendingHumanHandoff },
                        shopId);
                }
                catch { /* ignore */ }

                return Ok(new
                {
                    message = "已分配坐席",
                    sessionId = session.Id,
                    assignedAgent = new { id = agent.Id, name = agent.Name, role = agent.Role.ToString() },
                    assignedAt = session.AssignedAt,
                    status = session.Status.ToString(),
                    pendingHumanHandoff = session.PendingHumanHandoff
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 当前坐席认领自己（Agent / Supervisor）。Seller 请用 assign。
        /// </summary>
        [HttpPost("sessions/{id:guid}/claim")]
        public async Task<IActionResult> ClaimSession(Guid id)
        {
            try
            {
                var current = GetCurrentUser();
                if (!current.IsStaff)
                    return BadRequest(new { message = "仅坐席可认领；商家请使用分配" });

                var shopId = GetMerchantShopId();
                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });
                if (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Resolved)
                    return BadRequest(new { message = "已结束的会话不能认领" });

                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == current.UserId && a.ShopId == shopId);
                if (agent == null || !agent.IsActive)
                    return BadRequest(new { message = "当前账号不是本店有效坐席" });

                session.AssignToAgent(agent.Id);
                await _db.SaveChangesAsync();

                try
                {
                    await _audit.LogAsync(current.UserId, current.UserType, AuditActions.SessionClaim,
                        "ChatSession", session.Id.ToString(),
                        new { agentId = agent.Id, pendingHumanHandoff = session.PendingHumanHandoff },
                        shopId);
                }
                catch { /* ignore */ }

                return Ok(new
                {
                    message = "已认领",
                    sessionId = session.Id,
                    assignedAgent = new { id = agent.Id, name = agent.Name, role = agent.Role.ToString() },
                    assignedAt = session.AssignedAt,
                    status = session.Status.ToString(),
                    pendingHumanHandoff = session.PendingHumanHandoff
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
        /// 用量摘要（计费页诚实计数）：今日草稿/会话、已连店铺、订阅档位与额度。
        /// </summary>
        [HttpGet("usage")]
        public async Task<IActionResult> GetUsage()
        {
            try
            {
                var shopId = GetMerchantShopId();
                var now = DateTime.UtcNow;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

                var messagesThisMonth = await (
                    from m in _db.ChatMessages
                    join s in _db.ChatSessions on m.ChatSessionId equals s.Id
                    where s.ShopId == shopId && m.CreatedAt >= monthStart
                    select m.Id
                ).CountAsync();

                var sessionsThisMonth = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.CreatedAt >= monthStart);

                var sessionsToday = await _db.ChatSessions
                    .CountAsync(s => s.ShopId == shopId && s.CreatedAt >= dayStart);

                var draftsToday = await (
                    from d in _db.DraftMessages
                    join s in _db.ChatSessions on d.ChatSessionId equals s.Id
                    where s.ShopId == shopId && d.CreatedAt >= dayStart
                    select d.Id
                ).CountAsync();

                var connectedShops = await _db.PlatformConnections
                    .CountAsync(c => c.SellerId == shopId && c.IsActive);

                var seller = await _db.Sellers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == shopId);

                var aiToday = await _db.AiUsageLogs.AsNoTracking()
                    .Where(a => a.SellerId == shopId && a.CreatedAt >= dayStart)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        promptTokens = g.Sum(x => x.PromptTokens),
                        completionTokens = g.Sum(x => x.CompletionTokens),
                        estimatedCostUsd = g.Sum(x => x.EstimatedCostUsd),
                        calls = g.Count(),
                        estimatedCalls = g.Count(x => x.IsEstimated),
                        exactPrompt = g.Where(x => !x.IsEstimated).Sum(x => x.PromptTokens),
                        exactCompletion = g.Where(x => !x.IsEstimated).Sum(x => x.CompletionTokens),
                        estimatedPrompt = g.Where(x => x.IsEstimated).Sum(x => x.PromptTokens),
                        estimatedCompletion = g.Where(x => x.IsEstimated).Sum(x => x.CompletionTokens)
                    })
                    .FirstOrDefaultAsync();

                var aiMonth = await _db.AiUsageLogs.AsNoTracking()
                    .Where(a => a.SellerId == shopId && a.CreatedAt >= monthStart)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        promptTokens = g.Sum(x => x.PromptTokens),
                        completionTokens = g.Sum(x => x.CompletionTokens),
                        estimatedCostUsd = g.Sum(x => x.EstimatedCostUsd),
                        calls = g.Count(),
                        estimatedCalls = g.Count(x => x.IsEstimated),
                        exactPrompt = g.Where(x => !x.IsEstimated).Sum(x => x.PromptTokens),
                        exactCompletion = g.Where(x => !x.IsEstimated).Sum(x => x.CompletionTokens),
                        estimatedPrompt = g.Where(x => x.IsEstimated).Sum(x => x.PromptTokens),
                        estimatedCompletion = g.Where(x => x.IsEstimated).Sum(x => x.CompletionTokens)
                    })
                    .FirstOrDefaultAsync();

                var promptTokensToday = aiToday?.promptTokens ?? 0;
                var completionTokensToday = aiToday?.completionTokens ?? 0;
                var promptTokensThisMonth = aiMonth?.promptTokens ?? 0;
                var completionTokensThisMonth = aiMonth?.completionTokens ?? 0;
                var exactTokensToday = (aiToday?.exactPrompt ?? 0) + (aiToday?.exactCompletion ?? 0);
                var estimatedTokensToday = (aiToday?.estimatedPrompt ?? 0) + (aiToday?.estimatedCompletion ?? 0);
                var exactTokensThisMonth = (aiMonth?.exactPrompt ?? 0) + (aiMonth?.exactCompletion ?? 0);
                var estimatedTokensThisMonth = (aiMonth?.estimatedPrompt ?? 0) + (aiMonth?.estimatedCompletion ?? 0);

                var byPurpose = await _db.AiUsageLogs.AsNoTracking()
                    .Where(a => a.SellerId == shopId && a.CreatedAt >= monthStart)
                    .GroupBy(a => a.Purpose)
                    .Select(g => new
                    {
                        purpose = g.Key,
                        calls = g.Count(),
                        tokens = g.Sum(x => x.PromptTokens + x.CompletionTokens),
                        costUsd = g.Sum(x => x.EstimatedCostUsd)
                    })
                    .OrderByDescending(x => x.calls)
                    .ToListAsync();

                return Ok(new
                {
                    periodStart = monthStart,
                    periodEnd = now,
                    messagesThisMonth,
                    sessionsThisMonth,
                    sessionsToday,
                    draftsToday,
                    connectedShops,
                    quota = seller?.FreeQuota,
                    freeQuota = seller?.FreeQuota,
                    subscription = seller?.SubscriptionLevel,
                    subscriptionLevel = seller?.SubscriptionLevel,
                    subscriptionEnd = seller?.SubscriptionEnd,
                    // AI token 记账（无数据为 0，不伪造）
                    promptTokensToday,
                    completionTokensToday,
                    totalTokensToday = promptTokensToday + completionTokensToday,
                    exactTokensToday,
                    estimatedTokensToday,
                    isEstimatedSummaryToday = (aiToday?.estimatedCalls ?? 0) > 0,
                    estimatedCostUsdToday = aiToday?.estimatedCostUsd ?? 0m,
                    aiCallsToday = aiToday?.calls ?? 0,
                    promptTokensThisMonth,
                    completionTokensThisMonth,
                    totalTokensThisMonth = promptTokensThisMonth + completionTokensThisMonth,
                    exactTokensThisMonth,
                    estimatedTokensThisMonth,
                    isEstimatedSummaryThisMonth = (aiMonth?.estimatedCalls ?? 0) > 0,
                    estimatedCostUsdThisMonth = aiMonth?.estimatedCostUsd ?? 0m,
                    aiCallsThisMonth = aiMonth?.calls ?? 0,
                    byPurpose,
                    note = "exactTokens=模型 Usage；estimatedTokens=无 Usage 时 chars/4 粗估（IsEstimated）；byPurpose=本月按 Purpose 分桶"
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }



        /// <summary>
        /// 近 N 日会话量（按日聚合 ChatSession，真实数据；无数据返回含 0 的日期序列）。
        /// </summary>
        [HttpGet("usage/daily")]
        public async Task<IActionResult> GetUsageDaily([FromQuery] int days = 7)
        {
            try
            {
                if (days < 1) days = 1;
                if (days > 90) days = 90;

                var shopId = GetMerchantShopId();
                var now = DateTime.UtcNow;
                var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                var rangeStart = dayStart.AddDays(-(days - 1));

                var raw = await _db.ChatSessions.AsNoTracking()
                    .Where(s => s.ShopId == shopId && s.CreatedAt >= rangeStart)
                    .GroupBy(s => s.CreatedAt.Date)
                    .Select(g => new { date = g.Key, sessions = g.Count() })
                    .ToListAsync();

                var msgRaw = await (
                    from m in _db.ChatMessages.AsNoTracking()
                    join s in _db.ChatSessions.AsNoTracking() on m.ChatSessionId equals s.Id
                    where s.ShopId == shopId && m.CreatedAt >= rangeStart
                    group m by m.CreatedAt.Date into g
                    select new { date = g.Key, messages = g.Count() }
                ).ToListAsync();

                var aiRaw = await _db.AiUsageLogs.AsNoTracking()
                    .Where(a => a.SellerId == shopId && a.CreatedAt >= rangeStart)
                    .GroupBy(a => a.CreatedAt.Date)
                    .Select(g => new
                    {
                        date = g.Key,
                        aiCalls = g.Count(),
                        tokens = g.Sum(x => x.PromptTokens + x.CompletionTokens)
                    })
                    .ToListAsync();

                var byDate = raw.ToDictionary(x => x.date.Date, x => x.sessions);
                var msgByDate = msgRaw.ToDictionary(x => x.date.Date, x => x.messages);
                var aiByDate = aiRaw.ToDictionary(x => x.date.Date, x => x);

                var items = new List<object>();
                var anyNonZero = false;
                for (var i = 0; i < days; i++)
                {
                    var d = rangeStart.AddDays(i).Date;
                    byDate.TryGetValue(d, out var sessions);
                    msgByDate.TryGetValue(d, out var messages);
                    aiByDate.TryGetValue(d, out var ai);
                    if (sessions > 0 || messages > 0) anyNonZero = true;
                    items.Add(new
                    {
                        date = d.ToString("yyyy-MM-dd"),
                        count = sessions, // 兼容图表：会话数
                        sessions,
                        messages,
                        aiCalls = ai?.aiCalls ?? 0,
                        tokens = ai?.tokens ?? 0
                    });
                }

                return Ok(new
                {
                    days,
                    rangeStart,
                    rangeEnd = now,
                    items,
                    hasData = anyNonZero
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

                var slaAlerts = sessions
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
                            type = "sla",
                            sessionId = (Guid?)s.Id,
                            sessionBizId = (string?)s.SessionId,
                            customerName = (string?)s.CustomerName,
                            platform = (string?)s.Platform,
                            status = (string?)s.status,
                            pendingHumanHandoff = s.PendingHumanHandoff,
                            hoursSinceLastBuyerMsg = Math.Round(hours, 2),
                            thresholdHours = highest,
                            needsResponseBy = (DateTime?)needsBy,
                            slaUrgency = now >= needsBy ? "overdue" : "soon",
                            channel = "in-app",
                            connectionId = (Guid?)null,
                            tokenStatus = (string?)null,
                            message = (string?)null
                        };
                    })
                    .Where(a => a != null)
                    .Select(a => a!)
                    .ToList();

                // Token 过期/即将过期（24h）连接告警
                var conns = await _db.PlatformConnections
                    .AsNoTracking()
                    .Where(c => c.SellerId == shopId && c.IsActive)
                    .Select(c => new { c.Id, c.Platform, c.Nickname, c.ShopId, c.TokenExpiresAt, c.LastRefreshError })
                    .ToListAsync();
                var tokenAlerts = conns
                    .Select(c =>
                    {
                        if (!c.TokenExpiresAt.HasValue)
                            return null;
                        string tokenStatus;
                        if (c.TokenExpiresAt.Value <= now) tokenStatus = "expired";
                        else if (c.TokenExpiresAt.Value <= now.AddHours(24)) tokenStatus = "expiring";
                        else return null;
                        var hrs = Math.Round((c.TokenExpiresAt.Value - now).TotalHours, 2);
                        return new
                        {
                            type = "connection_token",
                            sessionId = (Guid?)null,
                            sessionBizId = (string?)null,
                            customerName = (string?)null,
                            platform = (string?)c.Platform,
                            status = (string?)tokenStatus,
                            pendingHumanHandoff = false,
                            hoursSinceLastBuyerMsg = 0.0,
                            thresholdHours = 24.0,
                            needsResponseBy = (DateTime?)c.TokenExpiresAt,
                            slaUrgency = tokenStatus == "expired" ? "overdue" : "soon",
                            channel = "in-app",
                            connectionId = (Guid?)c.Id,
                            tokenStatus = (string?)tokenStatus,
                            message = (string?)(tokenStatus == "expired" && !string.IsNullOrEmpty(c.LastRefreshError)
                                ? $"店铺 {c.Nickname ?? c.ShopId ?? c.Platform} 需重新授权"
                                : tokenStatus == "expired"
                                ? $"店铺 {c.Nickname ?? c.ShopId ?? c.Platform} Token 已过期"
                                : $"店铺 {c.Nickname ?? c.ShopId ?? c.Platform} Token 将在 {hrs:0.#} 小时内过期")
                        };
                    })
                    .Where(a => a != null)
                    .Select(a => a!)
                    .ToList();

                var alertItems = slaAlerts.Concat(tokenAlerts)
                    .OrderByDescending(a => a!.type == "connection_token" && a.status == "expired")
                    .ThenByDescending(a => a!.hoursSinceLastBuyerMsg)
                    .ToList();

                return Ok(new
                {
                    items = alertItems,
                    total = alertItems.Count,
                    slaCount = slaAlerts.Count,
                    tokenAlertCount = tokenAlerts.Count,
                    responseSlaHours = slaHours,
                    thresholds = hoursList,
                    // 应用内 + 浏览器 Notification；无 APNs/FCM Push
                    browserNotifySupported = true,
                    pushEnabled = false
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
                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.DraftReject,
                        "ChatSession", id.ToString(),
                        new { discarded = drafts.Count },
                        shopId);
                }
                catch { }
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

                var platformOutboundId = await client.SendReplyAsync(platformMsg, draft.Content);

                var aiMsg = ChatMessage.FromAI(draft.Content, chatSessionId: session.Id);
                // 优先写平台真实 message_id；无则 outbound:{localId} 兜底（见平台客户端注释）
                aiMsg.PlatformMsgId = !string.IsNullOrWhiteSpace(platformOutboundId)
                    ? platformOutboundId
                    : $"outbound:{aiMsg.Id:N}";
                _db.ChatMessages.Add(aiMsg);
                session.AddAiMessage();

                draft.Status = DraftStatuses.Sent;
                draft.SentAt = DateTime.UtcNow;
                draft.SentMessageId = aiMsg.Id;
                draft.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.DraftApprove,
                        "DraftMessage", draft.Id.ToString(),
                        new { sessionId = session.Id, messageId = aiMsg.Id },
                        shopId);
                }
                catch { }

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




        /// <summary>
        /// 本店操作审计日志（Seller / Supervisor；Admin 亦可按 shopId）。
        /// </summary>
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] int take = 50, [FromQuery] string? action = null)
        {
            try
            {
                if (!CanManageShopOwnerResources())
                    return Forbid();
                var shopId = GetShopOwnerSellerId();
                take = Math.Clamp(take, 1, 200);
                var q = _db.AuditLogs
                    .AsNoTracking()
                    .Where(a => a.ShopId == shopId);
                if (!string.IsNullOrWhiteSpace(action))
                    q = q.Where(a => a.Action == action.Trim());
                var items = await q
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(take)
                    .Select(a => new
                    {
                        a.Id,
                        a.ActorId,
                        a.ActorType,
                        a.Action,
                        a.ResourceType,
                        a.ResourceId,
                        a.DetailJson,
                        a.CreatedAt,
                        a.ShopId
                    })
                    .ToListAsync();
                return Ok(new { items, total = items.Count, take });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>快捷回复列表（本店 + 全局）。Seller/Supervisor/Admin。</summary>
        [HttpGet("quick-replies")]
        public async Task<IActionResult> ListQuickReplies([FromQuery] Guid? shopId = null)
        {
            try
            {
                // 坐席可读（插入草稿）；写操作仍限 Seller/Supervisor
                var sellerId = GetMerchantShopId();
                var sid = shopId ?? sellerId;
                if (sid != sellerId)
                    return Forbid();

                var items = await _db.QuickReplies.AsNoTracking()
                    .Where(q =>
                        (q.Scope == QuickReplyScope.Shop && q.ShopId == sellerId)
                        || q.Scope == QuickReplyScope.Global)
                    .OrderBy(q => q.SortOrder)
                    .ThenByDescending(q => q.CreatedAt)
                    .Select(q => new
                    {
                        id = q.Id,
                        title = q.Title,
                        content = q.Content,
                        category = q.Category.ToString(),
                        categoryValue = (int)q.Category,
                        keywords = q.Keywords,
                        scope = q.Scope.ToString(),
                        shopId = q.ShopId,
                        isActive = q.IsActive,
                        sortOrder = q.SortOrder,
                        createdAt = q.CreatedAt,
                        updatedAt = q.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { items, total = items.Count });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>创建店铺级快捷回复。Seller/Supervisor/Admin。</summary>
        [HttpPost("quick-replies")]
        public async Task<IActionResult> CreateQuickReply([FromBody] QuickReplyUpsertRequest body)
        {
            try
            {
                var sellerId = GetShopOwnerSellerId();
                if (body == null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Content))
                    return BadRequest(new { message = "title 与 content 必填" });

                var category = ParseQuickReplyCategory(body.Category);
                var entity = QuickReply.CreateForShop(sellerId, body.Title.Trim(), body.Content.Trim(), category);
                if (!string.IsNullOrWhiteSpace(body.Keywords))
                    entity.Update(entity.Title, entity.Content, body.Keywords.Trim());
                if (body.SortOrder.HasValue)
                    entity.SetSortOrder(body.SortOrder.Value);
                if (body.IsActive.HasValue)
                    entity.SetActive(body.IsActive.Value);

                _db.QuickReplies.Add(entity);
                await _db.SaveChangesAsync();
                return Ok(new { id = entity.Id, message = "已创建" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>更新快捷回复。Seller/Supervisor/Admin。</summary>
        [HttpPut("quick-replies/{id:guid}")]
        public async Task<IActionResult> UpdateQuickReply(Guid id, [FromBody] QuickReplyUpsertRequest body)
        {
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var entity = await _db.QuickReplies.FirstOrDefaultAsync(q => q.Id == id);
                if (entity == null)
                    return NotFound(new { message = "快捷回复不存在" });
                if (entity.Scope == QuickReplyScope.Shop && entity.ShopId != sellerId)
                    return StatusCode(403, new { message = "无权修改其他店铺的快捷回复" });
                if (entity.Scope == QuickReplyScope.Global && !GetCurrentUser().IsAdmin)
                    return StatusCode(403, new { message = "全局快捷回复仅 Admin 可改；请创建店铺级副本" });

                if (body == null || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Content))
                    return BadRequest(new { message = "title 与 content 必填" });

                var category = ParseQuickReplyCategory(body.Category);
                entity.Update(body.Title.Trim(), body.Content.Trim(), body.Keywords?.Trim(), category);
                if (body.SortOrder.HasValue)
                    entity.SetSortOrder(body.SortOrder.Value);
                if (body.IsActive.HasValue)
                    entity.SetActive(body.IsActive.Value);

                await _db.SaveChangesAsync();
                return Ok(new { id = entity.Id, message = "已更新" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>删除快捷回复。Seller/Supervisor/Admin。</summary>
        [HttpDelete("quick-replies/{id:guid}")]
        public async Task<IActionResult> DeleteQuickReply(Guid id)
        {
            try
            {
                var sellerId = GetShopOwnerSellerId();
                var entity = await _db.QuickReplies.FirstOrDefaultAsync(q => q.Id == id);
                if (entity == null)
                    return NotFound(new { message = "快捷回复不存在" });
                if (entity.Scope == QuickReplyScope.Shop && entity.ShopId != sellerId)
                    return StatusCode(403, new { message = "无权删除其他店铺的快捷回复" });
                if (entity.Scope == QuickReplyScope.Global && !GetCurrentUser().IsAdmin)
                    return StatusCode(403, new { message = "全局快捷回复仅 Admin 可删" });

                _db.QuickReplies.Remove(entity);
                await _db.SaveChangesAsync();
                return Ok(new { message = "已删除" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        private static QuickReplyCategory ParseQuickReplyCategory(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return QuickReplyCategory.General;
            if (int.TryParse(raw, out var n) && Enum.IsDefined(typeof(QuickReplyCategory), n))
                return (QuickReplyCategory)n;
            if (Enum.TryParse<QuickReplyCategory>(raw, true, out var c))
                return c;
            return QuickReplyCategory.General;
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

    public class QuickReplyUpsertRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsActive { get; set; }
    }

    public class MerchantAssignDto
    {
        public Guid AgentId { get; set; }
    }

}
