using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.Interfaces;
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
        private readonly IAuditLogger _audit;

        public AdminController(AppDbContext db, IAuditLogger audit)
        {
            _db = db;
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
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
                    calls = g.Count(),
                    estimatedCalls = g.Count(x => x.IsEstimated),
                    exactPrompt = g.Where(x => !x.IsEstimated).Sum(x => x.PromptTokens),
                    exactCompletion = g.Where(x => !x.IsEstimated).Sum(x => x.CompletionTokens),
                    estimatedPrompt = g.Where(x => x.IsEstimated).Sum(x => x.PromptTokens),
                    estimatedCompletion = g.Where(x => x.IsEstimated).Sum(x => x.CompletionTokens)
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
                .Where(a => a.CreatedAt >= monthStart)
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
                note = "含 AiUsageLog；exactTokens=模型 Usage，estimatedTokens=chars/4 粗估（IsEstimated）；byPurpose=本月按 Purpose 分桶。"
            });
        }

        /// <summary>全站近 N 日会话/消息/AI 调用日趋势</summary>
        [HttpGet("usage/daily")]
        public async Task<IActionResult> GetUsageDaily([FromQuery] int days = 7)
        {
            if (days < 1) days = 1;
            if (days > 90) days = 90;

            var now = DateTime.UtcNow;
            var dayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
            var rangeStart = dayStart.AddDays(-(days - 1));

            var sessionRaw = await _db.ChatSessions.AsNoTracking()
                .Where(s => s.CreatedAt >= rangeStart)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new { date = g.Key, sessions = g.Count() })
                .ToListAsync();

            var msgRaw = await _db.ChatMessages.AsNoTracking()
                .Where(m => m.CreatedAt >= rangeStart)
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new { date = g.Key, messages = g.Count() })
                .ToListAsync();

            var aiRaw = await _db.AiUsageLogs.AsNoTracking()
                .Where(a => a.CreatedAt >= rangeStart)
                .GroupBy(a => a.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key,
                    aiCalls = g.Count(),
                    tokens = g.Sum(x => x.PromptTokens + x.CompletionTokens)
                })
                .ToListAsync();

            var byS = sessionRaw.ToDictionary(x => x.date.Date, x => x.sessions);
            var byM = msgRaw.ToDictionary(x => x.date.Date, x => x.messages);
            var byA = aiRaw.ToDictionary(x => x.date.Date, x => x);

            var items = new List<object>();
            var anyNonZero = false;
            for (var i = 0; i < days; i++)
            {
                var d = rangeStart.AddDays(i).Date;
                byS.TryGetValue(d, out var sessions);
                byM.TryGetValue(d, out var messages);
                byA.TryGetValue(d, out var ai);
                if (sessions > 0 || messages > 0) anyNonZero = true;
                items.Add(new
                {
                    date = d.ToString("yyyy-MM-dd"),
                    count = sessions,
                    sessions,
                    messages,
                    aiCalls = ai?.aiCalls ?? 0,
                    tokens = ai?.tokens ?? 0
                });
            }

            return Ok(new { days, rangeStart, rangeEnd = now, items, hasData = anyNonZero });
        }

        /// <summary>只读配置说明（不写敏感密钥）</summary>

        /// <summary>全站审计日志（Admin）</summary>
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int take = 50,
            [FromQuery] Guid? shopId = null,
            [FromQuery] string? action = null)
        {
            take = Math.Clamp(take, 1, 200);
            var q = _db.AuditLogs.AsNoTracking().AsQueryable();
            if (shopId.HasValue)
                q = q.Where(a => a.ShopId == shopId.Value);
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


        /// <summary>禁用/启用商家（运营写操作，记审计）</summary>
        [HttpPatch("merchants/{id:guid}/active")]
        public async Task<IActionResult> SetMerchantActive(Guid id, [FromBody] AdminMerchantActiveDto dto)
        {
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == id);
            if (seller == null) return NotFound(new { message = "商家不存在" });

            var wasActive = seller.IsActive;
            seller.SetActive(dto.IsActive);
            await _db.SaveChangesAsync();

            var actorId = TryGetActorId();
            await _audit.LogAsync(
                actorId,
                "Admin",
                dto.IsActive ? AuditActions.AdminMerchantEnable : AuditActions.AdminMerchantDisable,
                "Seller",
                seller.Id.ToString(),
                new { wasActive, isActive = dto.IsActive, phone = seller.Phone },
                seller.Id);

            return Ok(new { id = seller.Id, isActive = seller.IsActive, message = dto.IsActive ? "已启用" : "已禁用" });
        }

        /// <summary>修改商家订阅等级（运营写操作，记审计）</summary>
        [HttpPatch("merchants/{id:guid}/subscription")]
        public async Task<IActionResult> SetMerchantSubscription(Guid id, [FromBody] AdminMerchantSubscriptionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Level))
                return BadRequest(new { message = "level 必填（Free/Basic/Pro）" });

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == id);
            if (seller == null) return NotFound(new { message = "商家不存在" });

            var prev = seller.SubscriptionLevel;
            try
            {
                seller.UpgradeSubscription(dto.Level.Trim());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            await _db.SaveChangesAsync();

            var actorId = TryGetActorId();
            await _audit.LogAsync(
                actorId,
                "Admin",
                AuditActions.AdminSubscriptionUpdate,
                "Seller",
                seller.Id.ToString(),
                new { previous = prev, level = seller.SubscriptionLevel },
                seller.Id);

            return Ok(new { id = seller.Id, subscriptionLevel = seller.SubscriptionLevel, message = "订阅已更新" });
        }

        private Guid? TryGetActorId()
        {
            var claim = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("userId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        /// <summary>
        /// 运营设置：只读说明 + DB 可覆盖的安全开关（无密钥）。
        /// </summary>
        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings([FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env)
        {
            var writable = await LoadWritableSettingsAsync(config);
            return Ok(new
            {
                environment = env.EnvironmentName,
                productPositioning = "工作台 + AI 草稿人审（draft-first），禁止全自动 chatbot 宣称",
                defaultOutboundMode = writable.DefaultOutboundMode,
                maintenanceMode = writable.MaintenanceMode,
                allowNewRegistration = writable.AllowNewRegistration,
                writableKeys = SystemSettingKeys.WritableKeys,
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
                },
                note = "仅 MaintenanceMode / DefaultOutboundMode / AllowNewRegistration 可写；密钥不可通过本接口读写"
            });
        }

        /// <summary>更新安全运营开关，审计 admin.settings.update</summary>
        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] AdminSettingsUpdateDto dto, [FromServices] IConfiguration config)
        {
            if (dto == null)
                return BadRequest(new { message = "body 不能为空" });

            var before = await LoadWritableSettingsAsync(config);
            var changed = new Dictionary<string, object?>();

            if (dto.MaintenanceMode.HasValue)
            {
                await UpsertSettingAsync(SystemSettingKeys.MaintenanceMode, dto.MaintenanceMode.Value ? "true" : "false");
                changed[SystemSettingKeys.MaintenanceMode] = dto.MaintenanceMode.Value;
            }
            if (!string.IsNullOrWhiteSpace(dto.DefaultOutboundMode))
            {
                var mode = OutboundModes.Normalize(dto.DefaultOutboundMode);
                await UpsertSettingAsync(SystemSettingKeys.DefaultOutboundMode, mode);
                changed[SystemSettingKeys.DefaultOutboundMode] = mode;
            }
            if (dto.AllowNewRegistration.HasValue)
            {
                await UpsertSettingAsync(SystemSettingKeys.AllowNewRegistration, dto.AllowNewRegistration.Value ? "true" : "false");
                changed[SystemSettingKeys.AllowNewRegistration] = dto.AllowNewRegistration.Value;
            }

            if (changed.Count == 0)
                return BadRequest(new { message = "未提供可写字段（maintenanceMode / defaultOutboundMode / allowNewRegistration）" });

            await _db.SaveChangesAsync();

            var actorId = TryGetActorId();
            await _audit.LogAsync(
                actorId,
                "Admin",
                AuditActions.AdminSettingsUpdate,
                "SystemSettings",
                null,
                new { before = new { before.MaintenanceMode, before.DefaultOutboundMode, before.AllowNewRegistration }, changed },
                null);

            var after = await LoadWritableSettingsAsync(config);
            return Ok(new
            {
                message = "设置已保存",
                maintenanceMode = after.MaintenanceMode,
                defaultOutboundMode = after.DefaultOutboundMode,
                allowNewRegistration = after.AllowNewRegistration
            });
        }

        private async Task UpsertSettingAsync(string key, string value)
        {
            var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (row == null)
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.Value = value;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        private async Task<(bool MaintenanceMode, string DefaultOutboundMode, bool AllowNewRegistration)> LoadWritableSettingsAsync(IConfiguration config)
        {
            var rows = await _db.SystemSettings.AsNoTracking()
                .Where(s => SystemSettingKeys.WritableKeys.Contains(s.Key))
                .ToListAsync();
            string? Get(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value;

            var maint = Get(SystemSettingKeys.MaintenanceMode)
                ?? config["Ops:MaintenanceMode"]
                ?? "false";
            var mode = Get(SystemSettingKeys.DefaultOutboundMode)
                ?? config["Ops:DefaultOutboundMode"]
                ?? OutboundModes.DraftFirst;
            var allowReg = Get(SystemSettingKeys.AllowNewRegistration)
                ?? config["Ops:AllowNewRegistration"]
                ?? "true";

            return (
                string.Equals(maint, "true", StringComparison.OrdinalIgnoreCase) || maint == "1",
                OutboundModes.Normalize(mode),
                string.Equals(allowReg, "true", StringComparison.OrdinalIgnoreCase) || allowReg == "1"
            );
        }
    }

    public class AdminMerchantActiveDto
    {
        public bool IsActive { get; set; }
    }

    public class AdminMerchantSubscriptionDto
    {
        public string Level { get; set; } = "Free";
    }

    public class AdminSettingsUpdateDto
    {
        public bool? MaintenanceMode { get; set; }
        public string? DefaultOutboundMode { get; set; }
        public bool? AllowNewRegistration { get; set; }
    }
}
