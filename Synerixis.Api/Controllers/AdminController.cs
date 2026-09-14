using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 运营 Admin 控制台只读 API（需 JWT Role=Admin）。
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseApiController
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Dashboard KPI：商家数、连接店铺、今日会话、待手审草稿、SLA overdue 粗计数</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var now = DateTime.UtcNow;
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var merchantCount = await _db.Sellers.CountAsync();
            var connectedShops = await _db.PlatformConnections.CountAsync(c => c.IsActive);
            var sessionsToday = await _db.ChatSessions.CountAsync(s => s.CreatedAt >= dayStart);
            var pendingDrafts = await _db.DraftMessages.CountAsync(d => d.Status == DraftStatuses.Pending);
            var handoffPending = await _db.ChatSessions.CountAsync(s => s.PendingHumanHandoff
                && s.Status != SessionStatus.Closed && s.Status != SessionStatus.Resolved);

            // SLA overdue 粗计数：有买家消息、未结束、按默认 12h 或店铺配置估算
            // 为性能：先拉未结束会话 + 配置字典，内存粗算
            var openSessions = await _db.ChatSessions
                .AsNoTracking()
                .Where(s => s.Status != SessionStatus.Closed && s.Status != SessionStatus.Resolved)
                .Select(s => new { s.ShopId, s.LastBuyerMessageAt, s.LastActiveAt, s.CreatedAt })
                .ToListAsync();
            var slaMap = await _db.SellerConfigs.AsNoTracking()
                .Where(c => c.SellerId != null)
                .Select(c => new { c.SellerId, c.ResponseSlaHours })
                .ToListAsync();
            var slaBySeller = slaMap
                .Where(x => x.SellerId.HasValue)
                .ToDictionary(x => x.SellerId!.Value, x => x.ResponseSlaHours > 0 ? x.ResponseSlaHours : 12);

            var slaOverdue = 0;
            foreach (var s in openSessions)
            {
                var hours = slaBySeller.TryGetValue(s.ShopId, out var h) ? h : 12;
                var anchor = s.LastBuyerMessageAt ?? s.LastActiveAt ?? s.CreatedAt;
                if (now >= anchor.AddHours(hours))
                    slaOverdue++;
            }

            return Ok(new
            {
                merchantCount,
                connectedShops,
                sessionsToday,
                pendingDrafts,
                handoffPending,
                slaOverdue,
                generatedAt = now
            });
        }

        /// <summary>商家分页列表</summary>
        [HttpGet("merchants")]
        public async Task<IActionResult> GetMerchants(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _db.Sellers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(s =>
                    (s.Phone != null && s.Phone.Contains(term))
                    || (s.Nickname != null && s.Nickname.Contains(term))
                    || (s.Email != null && s.Email.Contains(term)));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    id = s.Id,
                    phone = s.Phone,
                    nickname = s.Nickname,
                    email = s.Email,
                    subscriptionLevel = s.SubscriptionLevel,
                    freeQuota = s.FreeQuota,
                    subscriptionEnd = s.SubscriptionEnd,
                    isActive = s.IsActive,
                    createdAt = s.CreatedAt,
                    lastLoginAt = s.LastLoginAt,
                    connectionCount = _db.PlatformConnections.Count(c => c.SellerId == s.Id && c.IsActive)
                })
                .ToListAsync();

            return Ok(new { page, pageSize, total, items });
        }

        /// <summary>店铺连接列表</summary>
        [HttpGet("shops")]
        [HttpGet("connections")]
        public async Task<IActionResult> GetShops(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var query = _db.PlatformConnections.AsNoTracking();
            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    sellerId = c.SellerId,
                    platform = c.Platform,
                    shopId = c.ShopId,
                    openId = c.OpenId,
                    nickname = c.Nickname,
                    isActive = c.IsActive,
                    tokenExpiresAt = c.TokenExpiresAt,
                    createdAt = c.CreatedAt,
                    updatedAt = c.UpdatedAt,
                    sellerPhone = _db.Sellers.Where(s => s.Id == c.SellerId).Select(s => s.Phone).FirstOrDefault(),
                    sellerNickname = _db.Sellers.Where(s => s.Id == c.SellerId).Select(s => s.Nickname).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new { page, pageSize, total, items });
        }

        /// <summary>最近会话（只读）</summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(
            [FromQuery] int take = 50)
        {
            if (take < 1) take = 50;
            if (take > 200) take = 200;

            var items = await _db.ChatSessions.AsNoTracking()
                .OrderByDescending(s => s.LastActiveAt ?? s.CreatedAt)
                .Take(take)
                .Select(s => new
                {
                    id = s.Id,
                    shopId = s.ShopId,
                    platform = s.Platform,
                    customerId = s.CustomerId,
                    customerName = s.CustomerName,
                    status = s.Status.ToString(),
                    pendingHumanHandoff = s.PendingHumanHandoff,
                    handoffAt = s.HandoffAt,
                    messageCount = s.MessageCount,
                    lastBuyerMessageAt = s.LastBuyerMessageAt,
                    lastActiveAt = s.LastActiveAt,
                    createdAt = s.CreatedAt,
                    pendingDraftCount = _db.DraftMessages.Count(d =>
                        d.ChatSessionId == s.Id && d.Status == DraftStatuses.Pending)
                })
                .ToListAsync();

            return Ok(new { take, total = items.Count, items });
        }

        /// <summary>用量汇总（全站诚实聚合）</summary>
        [HttpGet("usage")]
        public async Task<IActionResult> GetUsage()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

            var messagesThisMonth = await _db.ChatMessages.CountAsync(m => m.CreatedAt >= monthStart);
            var messagesToday = await _db.ChatMessages.CountAsync(m => m.CreatedAt >= dayStart);
            var sessionsThisMonth = await _db.ChatSessions.CountAsync(s => s.CreatedAt >= monthStart);
            var sessionsToday = await _db.ChatSessions.CountAsync(s => s.CreatedAt >= dayStart);
            var draftsThisMonth = await _db.DraftMessages.CountAsync(d => d.CreatedAt >= monthStart);
            var draftsToday = await _db.DraftMessages.CountAsync(d => d.CreatedAt >= dayStart);
            var merchants = await _db.Sellers.CountAsync();
            var connectedShops = await _db.PlatformConnections.CountAsync(c => c.IsActive);

            var bySubscription = await _db.Sellers.AsNoTracking()
                .GroupBy(s => s.SubscriptionLevel)
                .Select(g => new { level = g.Key, count = g.Count(), totalQuota = g.Sum(x => x.FreeQuota ?? 0) })
                .ToListAsync();

            // 近 7 日会话量（真实；无数据则为空数组）
            var weekStart = dayStart.AddDays(-6);
            var dailySessions = await _db.ChatSessions.AsNoTracking()
                .Where(s => s.CreatedAt >= weekStart)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            var aiToday = await _db.AiUsageLogs.AsNoTracking()
                .Where(a => a.CreatedAt >= dayStart)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    promptTokens = g.Sum(x => x.PromptTokens),
                    completionTokens = g.Sum(x => x.CompletionTokens),
                    estimatedCostUsd = g.Sum(x => x.EstimatedCostUsd),
                    calls = g.Count()
                })
                .FirstOrDefaultAsync();

            var aiMonth = await _db.AiUsageLogs.AsNoTracking()
                .Where(a => a.CreatedAt >= monthStart)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    promptTokens = g.Sum(x => x.PromptTokens),
                    completionTokens = g.Sum(x => x.CompletionTokens),
                    estimatedCostUsd = g.Sum(x => x.EstimatedCostUsd),
                    calls = g.Count()
                })
                .FirstOrDefaultAsync();

            var promptTokensToday = aiToday?.promptTokens ?? 0;
            var completionTokensToday = aiToday?.completionTokens ?? 0;
            var promptTokensThisMonth = aiMonth?.promptTokens ?? 0;
            var completionTokensThisMonth = aiMonth?.completionTokens ?? 0;

            return Ok(new
            {
                periodStart = monthStart,
                periodEnd = now,
                messagesThisMonth,
                messagesToday,
                sessionsThisMonth,
                sessionsToday,
                draftsThisMonth,
                draftsToday,
                merchants,
                connectedShops,
                bySubscription,
                dailySessions,
                promptTokensToday,
                completionTokensToday,
                totalTokensToday = promptTokensToday + completionTokensToday,
                estimatedCostUsdToday = aiToday?.estimatedCostUsd ?? 0m,
                aiCallsToday = aiToday?.calls ?? 0,
                promptTokensThisMonth,
                completionTokensThisMonth,
                totalTokensThisMonth = promptTokensThisMonth + completionTokensThisMonth,
                estimatedCostUsdThisMonth = aiMonth?.estimatedCostUsd ?? 0m,
                aiCallsThisMonth = aiMonth?.calls ?? 0,
                note = "含 AiUsageLog token 合计与粗估费用；无调用记录时为 0。"
            });
        }

        /// <summary>只读配置说明（不写敏感密钥）</summary>
        [HttpGet("settings")]
        public IActionResult GetSettings([FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env)
        {
            return Ok(new
            {
                environment = env.EnvironmentName,
                productPositioning = "工作台 + AI 草稿人审（draft-first），禁止全自动 chatbot 宣称",
                defaultOutboundMode = OutboundModes.DraftFirst,
                adminAuth = "POST /api/auth/agent-login，需 Agents.Role=Admin",
                apiBaseHint = "/api/admin/*",
                frontends = new
                {
                    adminConsole = "http://localhost:3000 （Vite proxy /api → :5000）",
                    merchantWeb = config["Frontends:MerchantWebBaseUrl"] ?? "(Frontends:MerchantWebBaseUrl)"
                },
                features = new
                {
                    autoHandoffOnLowConfidence = true,
                    handoffOutsideBusinessHours = true,
                    webhookIdempotency = true,
                    slaAlerts = true,
                    slaSoundInMerchantWeb = true,
                    aiUsageLogging = true,
                    quickReplies = true,
                    browserNotification = true,
                    apnsFcmPush = false
                },
                health = new
                {
                    live = "/health",
                    ready = "/health/ready",
                    note = "ready 含 EF DbContext 连通检查"
                }
            });
        }
    }
}
