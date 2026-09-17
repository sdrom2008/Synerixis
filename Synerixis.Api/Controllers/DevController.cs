using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Services;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 仅 Development：无真实 Partner 时模拟进线 / 一键演示种子。
    /// </summary>
    [ApiController]
    [Route("api/dev")]
    public class DevController : BaseApiController
    {
        public const string DemoPhoneE164 = "+8613800138000";
        public const string DemoPhoneLocal = "13800138000";
        public const string DemoAgentEmail = "agent@demo.synerixis.local";
        public const string DemoAdminEmail = "admin@test.com";
        public const string DemoPassword = "Agent123!";

        private static readonly string[] DemoCustomerIds =
        {
            "demo-buyer-draft",
            "demo-buyer-handoff",
            "demo-buyer-normal",
            "demo-buyer-overdue",
            "demo-buyer-soon"
        };

        private readonly AppDbContext _db;
        private readonly IInboundSessionService _inbound;
        private readonly IInboundAiReplyService _inboundAi;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DevController> _logger;

        public DevController(
            AppDbContext db,
            IInboundSessionService inbound,
            IInboundAiReplyService inboundAi,
            IWebHostEnvironment env,
            ILogger<DevController> logger)
        {
            _db = db;
            _inbound = inbound;
            _inboundAi = inboundAi;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// 一键演示种子：固定商家 / 模拟店 / 会话 / 订单 / 坐席 / 快捷回复 / Admin。
        /// 幂等：重复调用跳过或刷新已存在演示数据。
        /// </summary>
        [HttpPost("seed-demo")]
        [AllowAnonymous]
        public async Task<IActionResult> SeedDemo(CancellationToken cancellationToken)
        {
            if (!_env.IsDevelopment())
                return NotFound(new { code = 404, message = "Dev endpoints are Development-only" });

            var created = new List<string>();
            var skipped = new List<string>();
            var updated = new List<string>();

            // 1) 固定演示商家
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == DemoPhoneE164, cancellationToken);
            if (seller == null)
            {
                seller = Seller.CreateWithPhone(DemoPhoneE164);
                seller.UpdateProfile("演示商家 Synerixis", null);
                _db.Sellers.Add(seller);
                await _db.SaveChangesAsync(cancellationToken);
                created.Add("seller");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(seller.Nickname) || seller.Nickname.StartsWith("商户"))
                {
                    seller.UpdateProfile("演示商家 Synerixis", null);
                    updated.Add("seller.nickname");
                }
                else
                {
                    skipped.Add("seller");
                }
            }

            // 2) SellerConfig
            var config = await _db.SellerConfigs.FirstOrDefaultAsync(c => c.SellerId == seller.Id, cancellationToken);
            if (config == null)
            {
                config = new SellerConfig
                {
                    SellerId = seller.Id,
                    ShopName = "演示商家 Synerixis",
                    DefaultReplyTone = "professional",
                    PreferredLanguage = "zh",
                    EnableAutoReply = true,
                    OutboundMode = OutboundModes.DraftFirst,
                    BusinessHoursStart = "09:00",
                    BusinessHoursEnd = "22:00",
                    ResponseSlaHours = 12,
                    AlertThresholdHours = "1,3,12",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow.AddSeconds(5) // 视为已定制，Onboarding 营业时间打勾
                };
                _db.SellerConfigs.Add(config);
                created.Add("sellerConfig");
            }
            else
            {
                // 演示默认 draft-first：若曾改成 AutoSend，seed 拉回 DraftFirst
                if (!string.Equals(config.OutboundMode, OutboundModes.DraftFirst, StringComparison.OrdinalIgnoreCase))
                {
                    config.OutboundMode = OutboundModes.DraftFirst;
                    config.UpdatedAt = DateTime.UtcNow;
                    updated.Add("sellerConfig.outboundMode");
                }
                else
                {
                    skipped.Add("sellerConfig");
                }
            }

            // 3) 模拟 Shopee + TikTok PlatformConnection（多店筛选）
            var connection = await EnsureSimConnectionAsync(seller.Id, "SHOPEE", cancellationToken);
            if (connection.Nickname != "模拟 Shopee 店")
            {
                connection.UpdateProfile("模拟 Shopee 店", null);
                updated.Add("platformConnection.profile");
            }
            else
            {
                skipped.Add("platformConnection");
            }

            var tiktokConnection = await EnsureSimConnectionAsync(seller.Id, "TIKTOK", cancellationToken);
            if (tiktokConnection.Nickname != "模拟 TikTok 店")
            {
                tiktokConnection.UpdateProfile("模拟 TikTok 店", null);
                updated.Add("platformConnection.tiktok.profile");
            }
            else
            {
                skipped.Add("platformConnection.tiktok");
            }

            // 4) 坐席（店铺 Agent）+ Admin（控制台）
            var shopAgent = await EnsureAgentAsync(
                seller.Id,
                DemoAgentEmail,
                "演示坐席小美",
                AgentRole.Agent,
                created,
                skipped,
                updated,
                cancellationToken);

            await EnsureAgentAsync(
                seller.Id,
                DemoAdminEmail,
                "Admin Agent",
                AgentRole.Admin,
                created,
                skipped,
                updated,
                cancellationToken);

            // 5) QuickReply
            await EnsureQuickRepliesAsync(seller.Id, created, skipped, cancellationToken);

            // 6) 会话 ×3
            var draftSession = await EnsureSessionBundleAsync(
                seller.Id,
                connection,
                shopAgent.Id,
                kind: "draft",
                customerId: DemoCustomerIds[0],
                customerName: "演示买家·待审草稿",
                platform: "SHOPEE",
                created,
                skipped,
                updated,
                cancellationToken);

            var handoffSession = await EnsureSessionBundleAsync(
                seller.Id,
                connection,
                shopAgent.Id,
                kind: "handoff",
                customerId: DemoCustomerIds[1],
                customerName: "演示买家·转人工",
                platform: "SHOPEE",
                created,
                skipped,
                updated,
                cancellationToken);

            var normalSession = await EnsureSessionBundleAsync(
                seller.Id,
                connection,
                shopAgent.Id,
                kind: "normal",
                customerId: DemoCustomerIds[2],
                customerName: "演示买家·正常咨询",
                platform: "SHOPEE",
                created,
                skipped,
                updated,
                cancellationToken);

            // 第二店（TikTok）一条待审草稿，便于收件箱「店铺筛选」演示
            var tiktokDraftSession = await EnsureSessionBundleAsync(
                seller.Id,
                tiktokConnection,
                shopAgent.Id,
                kind: "draft",
                customerId: "demo-buyer-tiktok",
                customerName: "演示买家·TikTok店",
                platform: "TIKTOK",
                created,
                skipped,
                updated,
                cancellationToken);

            // SLA 已超时样例：收件箱「超时告警」可点开演示
            var overdueSession = await EnsureSessionBundleAsync(
                seller.Id,
                connection,
                shopAgent.Id,
                kind: "overdue",
                customerId: DemoCustomerIds[3],
                customerName: "演示买家·已超时",
                platform: "SHOPEE",
                created,
                skipped,
                updated,
                cancellationToken);

            // SLA 即将超时样例（约剩 0.3h，橙标）
            var soonSession = await EnsureSessionBundleAsync(
                seller.Id,
                connection,
                shopAgent.Id,
                kind: "soon",
                customerId: DemoCustomerIds[4],
                customerName: "演示买家·即将超时",
                platform: "SHOPEE",
                created,
                skipped,
                updated,
                cancellationToken);

            // 7) 本地订单（挂 draft / normal 买家，便于 Inbox 侧栏）
            await EnsureOrderAsync(
                seller.Id,
                "DEMO-ORD-001",
                DemoCustomerIds[0],
                "演示买家·待审草稿",
                "SF1432887654321",
                "顺丰速运",
                "Shipped",
                129.90m,
                created,
                skipped,
                updated,
                cancellationToken);

            await EnsureOrderAsync(
                seller.Id,
                "DEMO-ORD-002",
                DemoCustomerIds[2],
                "演示买家·正常咨询",
                "YT9876543210123",
                "圆通速递",
                "Paid",
                59.00m,
                created,
                skipped,
                updated,
                cancellationToken);

            // 8) 用量（Admin usage 非空）
            var usageCount = await _db.AiUsageLogs.CountAsync(u => u.SellerId == seller.Id, cancellationToken);
            if (usageCount == 0)
            {
                _db.AiUsageLogs.Add(new AiUsageLog
                {
                    SellerId = seller.Id,
                    SessionId = draftSession.Id,
                    Model = "qwen-max",
                    PromptTokens = 320,
                    CompletionTokens = 180,
                    EstimatedCostUsd = 0.002m,
                    Purpose = AiUsagePurposes.Draft,
                    IsEstimated = true,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-30)
                });
                _db.AiUsageLogs.Add(new AiUsageLog
                {
                    SellerId = seller.Id,
                    SessionId = normalSession.Id,
                    Model = "qwen-max",
                    PromptTokens = 210,
                    CompletionTokens = 95,
                    EstimatedCostUsd = 0.001m,
                    Purpose = AiUsagePurposes.Classify,
                    IsEstimated = true,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                });
                created.Add("aiUsageLogs");
            }
            else
            {
                skipped.Add("aiUsageLogs");
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[Dev] seed-demo done seller={Seller} created={Created} skipped={Skipped} updated={Updated}",
                seller.Id, string.Join(',', created), string.Join(',', skipped), string.Join(',', updated));

            return Ok(new
            {
                code = 0,
                message = "ok",
                sellerId = seller.Id,
                phone = DemoPhoneLocal,
                phoneE164 = DemoPhoneE164,
                phoneLoginHint = $"手机号 {DemoPhoneLocal}，验证码 123456",
                agent = new { email = DemoAgentEmail, password = DemoPassword },
                admin = new { email = DemoAdminEmail, password = DemoPassword, hint = "Admin 控制台 agent-login；亦可 POST /api/auth/init-agent" },
                connection = new
                {
                    id = connection.Id,
                    platform = connection.Platform,
                    shopId = connection.ShopId,
                    nickname = connection.Nickname
                },
                sessions = new
                {
                    draft = draftSession.Id,
                    handoff = handoffSession.Id,
                    normal = normalSession.Id,
                    tiktokDraft = tiktokDraftSession.Id,
                    overdue = overdueSession.Id,
                    soon = soonSession.Id
                },
                shops = new
                {
                    shopee = new { id = connection.Id, nickname = connection.Nickname, shopId = connection.ShopId },
                    tiktok = new { id = tiktokConnection.Id, nickname = tiktokConnection.Nickname, shopId = tiktokConnection.ShopId }
                },
                created,
                skipped,
                updated,
                next = new[]
                {
                    "merchant-web：手机登录 " + DemoPhoneLocal + " / 123456",
                    "收件箱：待发草稿 → 审核发送（SIM 店 mock 出站）",
                    "收件箱：超时告警 → 已超时 / 即将超时；待人工 → 旧草稿仍可发；可手动起草发送",
                    "坐席登录 " + DemoAgentEmail + " / " + DemoPassword,
                    "admin-console：" + DemoAdminEmail + " / " + DemoPassword,
                    "POST /api/dev/simulate-inbound 给当前商家再注入一条"
                }
            });
        }

        /// <summary>
        /// 注入一条模拟买家消息。AllowAnonymous（body 带 sellerId）或 Seller JWT。
        /// </summary>
        [HttpPost("simulate-inbound")]
        [AllowAnonymous]
        public async Task<IActionResult> SimulateInbound(
            [FromBody] SimulateInboundRequest? request,
            CancellationToken cancellationToken)
        {
            if (!_env.IsDevelopment())
                return NotFound(new { code = 404, message = "Dev endpoints are Development-only" });

            request ??= new SimulateInboundRequest();
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { code = 400, message = "message is required" });

            Guid sellerId;
            try
            {
                sellerId = ResolveSellerId(request.SellerId);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { code = 401, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { code = 400, message = ex.Message });
            }

            var seller = await _db.Sellers.FindAsync(new object[] { sellerId }, cancellationToken);
            if (seller == null)
                return NotFound(new { code = 404, message = $"Seller {sellerId} not found" });

            var platform = string.IsNullOrWhiteSpace(request.Platform)
                ? "SHOPEE"
                : request.Platform.Trim().ToUpperInvariant();
            if (platform is not ("SHOPEE" or "TIKTOK"))
                return BadRequest(new { code = 400, message = "platform must be SHOPEE or TIKTOK" });

            var connection = await EnsureSimConnectionAsync(sellerId, platform, cancellationToken);
            var customerId = string.IsNullOrWhiteSpace(request.CustomerId)
                ? $"sim-buyer-{sellerId:N}"[..Math.Min(32, $"sim-buyer-{sellerId:N}".Length)]
                : request.CustomerId.Trim();
            var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
                ? "模拟买家"
                : request.CustomerName.Trim();

            var shopKey = connection.ShopId ?? connection.OpenId;
            var msg = new PlatformMessage
            {
                Platform = platform,
                OpenId = shopKey ?? string.Empty,
                CustomerId = customerId,
                CustomerName = customerName,
                Content = request.Message.Trim(),
                MessageType = "text",
                MsgId = $"sim-{Guid.NewGuid():N}",
                ConversationId = $"sim-conv-{customerId}",
                CreatedAt = DateTime.UtcNow
            };

            ChatSession? session;
            try
            {
                session = await _db.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(
                        s => s.Platform == platform
                             && s.CustomerId == customerId
                             && s.ShopId == sellerId,
                        cancellationToken);

                if (session == null)
                {
                    session = await _inbound.FindOrCreateSessionAsync(msg, cancellationToken);
                    if (session != null && session.ShopId != sellerId)
                    {
                        _logger.LogWarning(
                            "[Dev] Existing session {SessionId} belongs to other shop {Other}; creating for seller {Seller}",
                            session.Id, session.ShopId, sellerId);
                        session = ChatSession.Create(
                            shopId: sellerId,
                            platform: platform,
                            customerId: customerId,
                            customerName: customerName);
                        _db.ChatSessions.Add(session);
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }

                if (session == null)
                    return StatusCode(500, new { code = 500, message = "session_create_failed" });

                await _inbound.AppendBuyerMessageAsync(session, msg, cancellationToken);

                await _inboundAi.ProcessAfterBuyerMessageAsync(
                    session.Id, msg, msg.Content, cancellationToken);

                var draft = await _db.DraftMessages
                    .AsNoTracking()
                    .Where(d => d.ChatSessionId == session.Id && d.Status == DraftStatuses.Pending)
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var refreshed = await _db.ChatSessions.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken);

                return Ok(new
                {
                    code = 0,
                    message = "ok",
                    sessionId = session.Id,
                    sessionNo = session.SessionId,
                    customerId,
                    platform,
                    connectionId = connection.Id,
                    shopId = connection.ShopId,
                    shopNickname = connection.Nickname,
                    pendingHumanHandoff = refreshed?.PendingHumanHandoff ?? session.PendingHumanHandoff,
                    sessionStatus = refreshed?.Status.ToString() ?? session.Status.ToString(),
                    draft = draft == null
                        ? null
                        : new
                        {
                            id = draft.Id,
                            contentPreview = draft.Content.Length > 120
                                ? draft.Content[..120] + "…"
                                : draft.Content,
                            content = draft.Content,
                            status = draft.Status,
                            createdAt = draft.CreatedAt
                        }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Dev] simulate-inbound failed seller={Seller}", sellerId);
                var fallback = await _db.ChatSessions.AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Platform == platform && s.CustomerId == customerId && s.ShopId == sellerId,
                        cancellationToken);
                if (fallback != null)
                {
                    return Ok(new
                    {
                        code = 0,
                        message = "partial_ok_ai_degraded",
                        sessionId = fallback.Id,
                        warning = ex.Message,
                        draft = (object?)null
                    });
                }
                return StatusCode(500, new { code = 500, message = "simulate_failed", error = ex.Message });
            }
        }

        private Guid ResolveSellerId(string? bodySellerId)
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    return GetMerchantShopId();
                }
                catch
                {
                    // 已登录但非商户角色时，若 body 有 sellerId 仍允许
                }
            }

            if (!string.IsNullOrWhiteSpace(bodySellerId) && Guid.TryParse(bodySellerId.Trim(), out var sid))
                return sid;

            if (User?.Identity?.IsAuthenticated == true)
                throw new UnauthorizedAccessException("无法从 JWT 解析店铺；请传 sellerId");

            throw new UnauthorizedAccessException("Development：请登录商家账号，或在 body 中传 sellerId");
        }

        private async Task<PlatformConnection> EnsureSimConnectionAsync(
            Guid sellerId, string platform, CancellationToken cancellationToken)
        {
            var existing = await _db.Set<PlatformConnection>()
                .FirstOrDefaultAsync(
                    c => c.SellerId == sellerId
                         && c.Platform == platform
                         && c.IsActive
                         && c.ShopId != null
                         && c.ShopId.StartsWith("SIM-SHOP-"),
                    cancellationToken);

            if (existing != null)
            {
                if (existing.TokenExpiresAt == null || existing.TokenExpiresAt < DateTime.UtcNow.AddYears(1))
                {
                    existing.UpdateToken(
                        existing.AccessToken,
                        existing.RefreshToken ?? "sim-refresh-token",
                        DateTime.UtcNow.AddYears(10));
                    await _db.SaveChangesAsync(cancellationToken);
                }
                return existing;
            }

            // 按平台区分 ShopId，避免多店筛选时 OpenId 撞车
            var platTag = platform.Length >= 2 ? platform[..2] : platform;
            var rawShopId = $"SIM-SHOP-{platTag}-{sellerId:N}";
            var shopId = rawShopId[..Math.Min(32, rawShopId.Length)];
            var nickname = platform == "TIKTOK" ? "模拟 TikTok 店" : "模拟 Shopee 店";
            var conn = PlatformConnection.Create(
                sellerId: sellerId,
                platform: platform,
                appKey: "SIM-DEV",
                accessToken: "sim-access-token-not-for-production",
                openId: shopId,
                shopId: shopId,
                nickname: nickname,
                avatarUrl: null,
                region: "SG");
            conn.UpdateToken(
                "sim-access-token-not-for-production",
                "sim-refresh-token",
                DateTime.UtcNow.AddYears(10));
            _db.Set<PlatformConnection>().Add(conn);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "[Dev] Created sim PlatformConnection {Id} shop={Shop} seller={Seller} platform={Platform}",
                conn.Id, shopId, sellerId, platform);
            return conn;
        }

        private async Task<Agent> EnsureAgentAsync(
            Guid shopId,
            string email,
            string name,
            AgentRole role,
            List<string> created,
            List<string> skipped,
            List<string> updated,
            CancellationToken cancellationToken)
        {
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
            var hash = AgentPasswordHasher.Hash(DemoPassword);
            if (agent == null)
            {
                agent = Agent.Create(shopId, email, name, hash, role);
                _db.Agents.Add(agent);
                created.Add($"agent:{email}");
                return agent;
            }

            // 已存在：保证挂在演示店、密码可用、角色足够
            var dirty = false;
            if (agent.ShopId != shopId && role == AgentRole.Admin)
            {
                // Admin 可跨店查看；保留原 ShopId，不强制迁移
                skipped.Add($"agent:{email}:shop");
            }
            else if (agent.ShopId != shopId)
            {
                // 店铺坐席：若邮箱冲突且挂在别店，跳过改绑以免误伤
                skipped.Add($"agent:{email}:otherShop");
                return agent;
            }

            if (!AgentPasswordHasher.Verify(DemoPassword, agent.PasswordHash))
            {
                agent.UpdatePassword(hash);
                dirty = true;
                updated.Add($"agent:{email}:password");
            }

            if (agent.Role != role && role == AgentRole.Admin && agent.Role != AgentRole.Admin)
            {
                agent.UpdateRole(AgentRole.Admin);
                dirty = true;
                updated.Add($"agent:{email}:role");
            }

            if (!agent.IsActive)
            {
                agent.SetActive(true);
                dirty = true;
                updated.Add($"agent:{email}:active");
            }

            if (!dirty)
                skipped.Add($"agent:{email}");

            return agent;
        }

        private async Task EnsureQuickRepliesAsync(
            Guid shopId,
            List<string> created,
            List<string> skipped,
            CancellationToken cancellationToken)
        {
            var templates = new (string Title, string Content, QuickReplyCategory Cat)[]
            {
                ("[Demo] 欢迎语", "您好，欢迎光临演示店！请问有什么可以帮您？", QuickReplyCategory.PreSale),
                ("[Demo] 查物流", "您的订单已发出，快递单号可在会话右侧订单卡片查看，如需催件请告知。", QuickReplyCategory.Logistics),
                ("[Demo] 转人工说明", "已为您转接人工坐席，请稍候，我们会尽快回复。", QuickReplyCategory.AfterSale),
            };

            foreach (var t in templates)
            {
                var exists = await _db.QuickReplies.AnyAsync(
                    q => q.ShopId == shopId && q.Title == t.Title, cancellationToken);
                if (exists)
                {
                    skipped.Add($"quickReply:{t.Title}");
                    continue;
                }

                var qr = QuickReply.CreateForShop(shopId, t.Title, t.Content, t.Cat);
                qr.Update(t.Title, t.Content, keywords: "演示,demo", category: t.Cat);
                _db.QuickReplies.Add(qr);
                created.Add($"quickReply:{t.Title}");
            }
        }

        private async Task<ChatSession> EnsureSessionBundleAsync(
            Guid sellerId,
            PlatformConnection connection,
            Guid agentId,
            string kind,
            string customerId,
            string customerName,
            string platform,
            List<string> created,
            List<string> skipped,
            List<string> updated,
            CancellationToken cancellationToken)
        {
            platform = string.IsNullOrWhiteSpace(platform) ? "SHOPEE" : platform.Trim().ToUpperInvariant();
            var session = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(
                    s => s.ShopId == sellerId && s.Platform == platform && s.CustomerId == customerId,
                    cancellationToken);

            var shopOpenId = connection.ShopId ?? connection.OpenId;
            if (session == null)
            {
                session = ChatSession.Create(sellerId, platform, customerId, customerName);
                session.UpdatePlatformReplyContext($"demo-conv-{customerId}", shopOpenId);
                _db.ChatSessions.Add(session);
                await _db.SaveChangesAsync(cancellationToken);
                created.Add($"session:{kind}");

                if (kind == "draft")
                {
                    var buyer = ChatMessage.FromUser("你好，请问这款有货吗？多久能发货？", session.Id);
                    _db.ChatMessages.Add(buyer);
                    session.AddUserMessage();

                    _db.DraftMessages.Add(new DraftMessage
                    {
                        ChatSessionId = session.Id,
                        Content = "您好！该款现货充足，一般付款后 24 小时内发出（演示草稿，请人审后发送）。如需指定物流可告知。",
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow
                    });
                    session.AddAiMessage();
                }
                else if (kind == "handoff")
                {
                    var buyer = ChatMessage.FromUser("我要退款！已经投诉了，请马上处理！", session.Id);
                    _db.ChatMessages.Add(buyer);
                    session.AddUserMessage();
                    session.TransferToAgent();
                    _db.ChatMessages.Add(ChatMessage.FromAI(
                        "已触发转人工（演示）：敏感意图/投诉，请坐席接手。",
                        chatSessionId: session.Id));
                    session.AddAiMessage();
                    // 旧草稿保留：演示「转人工后仍可人审发送」
                    _db.DraftMessages.Add(new DraftMessage
                    {
                        ChatSessionId = session.Id,
                        Content = "（演示·转人工前草稿）非常抱歉给您带来不便，我们已安排专人跟进退款，请提供订单号后优先处理。",
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-8)
                    });
                }
                else if (kind == "overdue")
                {
                    var buyerAt = DateTime.UtcNow.AddHours(-14);
                    var buyer = ChatMessage.FromUser(
                        "你好，我的包裹已经两天没更新物流了，能帮忙催一下吗？",
                        session.Id);
                    buyer.CreatedAt = buyerAt;
                    _db.ChatMessages.Add(buyer);
                    session.AddUserMessage();
                    session.SeedBackdateBuyerActivity(buyerAt);
                    _db.DraftMessages.Add(new DraftMessage
                    {
                        ChatSessionId = session.Id,
                        Content = "（演示·超时待审）您好，已帮您催促承运商，最新轨迹预计今日更新；如仍无进展请回复本会话，我们继续跟进。",
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow.AddHours(-13)
                    });
                    session.AddAiMessage();
                }
                else if (kind == "soon")
                {
                    // 12h SLA：买家消息约 11.7h 前 → 即将超时（未 overdue）
                    var buyerAt = DateTime.UtcNow.AddHours(-11.7);
                    var buyer = ChatMessage.FromUser(
                        "在吗？想确认一下今天下单能否今天发出？",
                        session.Id);
                    buyer.CreatedAt = buyerAt;
                    _db.ChatMessages.Add(buyer);
                    session.AddUserMessage();
                    session.SeedBackdateBuyerActivity(buyerAt);
                    _db.DraftMessages.Add(new DraftMessage
                    {
                        ChatSessionId = session.Id,
                        Content = "（演示·即将超时）您好，今天下单可当天发出，一般 24 小时内揽收，请人审后尽快回复以免超时。",
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow.AddHours(-11.5)
                    });
                    session.AddAiMessage();
                }
                else // normal
                {
                    var buyer = ChatMessage.FromUser("订单什么时候到？单号发我一下谢谢。", session.Id);
                    _db.ChatMessages.Add(buyer);
                    session.AddUserMessage();

                    session.AssignToAgent(agentId);
                    var agentMsg = ChatMessage.FromAgent(
                        "您好，您的订单 DEMO-ORD-002 已支付，快递单号 YT9876543210123（圆通），预计 2–4 日送达。",
                        session.Id);
                    agentMsg.SenderId = agentId;
                    _db.ChatMessages.Add(agentMsg);
                    session.AddAgentMessage();
                }
            }
            else
            {
                // 幂等刷新：保证 handoff 闸 / 待审草稿存在
                session.UpdatePlatformReplyContext($"demo-conv-{customerId}", shopOpenId);
                if (kind == "handoff")
                {
                    if (!session.PendingHumanHandoff && session.Status != SessionStatus.Closed)
                    {
                        session.TransferToAgent();
                        updated.Add($"session:{kind}:handoff");
                    }
                    var hasPending = await _db.DraftMessages.AnyAsync(
                        d => d.ChatSessionId == session.Id
                             && (d.Status == DraftStatuses.Pending || d.Status == DraftStatuses.Superseded),
                        cancellationToken);
                    if (!hasPending)
                    {
                        _db.DraftMessages.Add(new DraftMessage
                        {
                            ChatSessionId = session.Id,
                            Content = "（演示·转人工前草稿）非常抱歉给您带来不便，我们已安排专人跟进退款，请提供订单号后优先处理。",
                            Status = DraftStatuses.Pending,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-8)
                        });
                        updated.Add($"session:{kind}:draft");
                    }
                    else if (!updated.Any(u => u.StartsWith($"session:{kind}:")))
                    {
                        skipped.Add($"session:{kind}");
                    }
                }
                else if (kind == "draft" || kind == "overdue" || kind == "soon")
                {
                    var hasPending = await _db.DraftMessages.AnyAsync(
                        d => d.ChatSessionId == session.Id && d.Status == DraftStatuses.Pending,
                        cancellationToken);
                    if (!hasPending)
                    {
                        var content = kind == "overdue"
                            ? "（演示·超时待审）您好，已帮您催促承运商，最新轨迹预计今日更新；如仍无进展请回复本会话，我们继续跟进。"
                            : kind == "soon"
                                ? "（演示·即将超时）您好，今天下单可当天发出，一般 24 小时内揽收，请人审后尽快回复以免超时。"
                                : "（演示）您好，这是刷新后的待审草稿，请确认后发送。";
                        var createdAt = kind == "overdue"
                            ? DateTime.UtcNow.AddHours(-13)
                            : kind == "soon"
                                ? DateTime.UtcNow.AddHours(-11.5)
                                : DateTime.UtcNow;
                        _db.DraftMessages.Add(new DraftMessage
                        {
                            ChatSessionId = session.Id,
                            Content = content,
                            Status = DraftStatuses.Pending,
                            CreatedAt = createdAt
                        });
                        updated.Add($"session:{kind}:draft");
                    }
                    else
                    {
                        skipped.Add($"session:{kind}");
                    }
                    if (kind == "overdue")
                    {
                        // 幂等：每次 seed 保持「已超时」态，便于演示 SLA 筛选
                        session.SeedBackdateBuyerActivity(DateTime.UtcNow.AddHours(-14));
                        updated.Add($"session:{kind}:sla");
                    }
                    else if (kind == "soon")
                    {
                        session.SeedBackdateBuyerActivity(DateTime.UtcNow.AddHours(-11.7));
                        updated.Add($"session:{kind}:sla");
                    }
                }
                else if (kind == "normal" && session.AssignedAgentId == null
                         && session.Status != SessionStatus.Closed
                         && session.Status != SessionStatus.Resolved)
                {
                    session.AssignToAgent(agentId);
                    updated.Add($"session:{kind}:assign");
                }
                else
                {
                    skipped.Add($"session:{kind}");
                }
            }

            return session;
        }

        private async Task EnsureOrderAsync(
            Guid shopId,
            string orderNo,
            string customerId,
            string customerName,
            string logisticsNo,
            string logisticsCompany,
            string status,
            decimal amount,
            List<string> created,
            List<string> skipped,
            List<string> updated,
            CancellationToken cancellationToken)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(
                o => o.ShopId == shopId && o.OrderNo == orderNo, cancellationToken);
            if (order == null)
            {
                order = Order.Create(shopId, orderNo, customerId, customerName, amount);
                order.UpdateStatus(status);
                order.UpdateLogistics(logisticsNo, logisticsCompany);
                order.ApplyLocalDemoDetails(
                    platform: "SHOPEE",
                    paymentAmount: amount,
                    customerPhone: "+6590000001",
                    shippingAddress: "Demo Address, Singapore",
                    externalOrderId: orderNo);
                _db.Orders.Add(order);
                created.Add($"order:{orderNo}");
            }
            else
            {
                order.UpdateLogistics(logisticsNo, logisticsCompany);
                order.ApplyLocalDemoDetails(
                    platform: "SHOPEE",
                    paymentAmount: amount,
                    customerPhone: "+6590000001",
                    shippingAddress: "Demo Address, Singapore",
                    externalOrderId: orderNo);
                updated.Add($"order:{orderNo}");
            }
        }
    }

    public class SimulateInboundRequest
    {
        public string? SellerId { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Platform { get; set; }
    }
}
