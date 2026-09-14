using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synerixis.Application.DTOs;
using Synerixis.Application.Helpers;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;
using ChatMessage = Synerixis.Domain.Entities.ChatMessage;
using Synerixis.Infrastructure.Clients;
using Synerixis.Infrastructure.Data;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 通用平台 Webhook 接收控制器
    /// 路由: POST /api/webhook/{platform} (通用)
    ///       POST /api/webhook/tiktok/test (TikTok 测试)
    ///       POST /api/webhook/shopee/test (Shopee 测试)
    /// </summary>
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly IPlatformClientRouter _router;
        private readonly IConversationRepository _conversationRepo;
        private readonly ILogger<WebhookController> _logger;
        private readonly AppDbContext _db;
        private readonly IServiceScopeFactory _scopeFactory;

        public WebhookController(
            IPlatformClientRouter router,
            IConversationRepository conversationRepo,
            AppDbContext db,
            IServiceScopeFactory scopeFactory,
            ILogger<WebhookController> logger)
        {
            _router = router;
            _conversationRepo = conversationRepo;
            _db = db;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        #region === 通用 Webhook 入口 ===

        /// <summary>
        /// 通用平台 Webhook 接收端点
        /// 支持签名验证 + 幂等性检查 + 事件类型路由
        /// </summary>
        [HttpPost("{platform}")]
        public async Task<IActionResult> Handle(string platform)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("[Webhook] Incoming from {Platform} at {RemoteIp}",
                    platform, HttpContext.Connection.RemoteIpAddress);

                // 1. 获取对应平台的客户端
                var client = _router.GetClient(platform);

                // 2. 验证签名（TikTok 支持新旧双格式）
                if (!await client.VerifySignatureAsync(Request))
                {
                    _logger.LogWarning("[Webhook] Signature verification failed for {Platform}", platform);
                    return BadRequest(new { code = 401, message = "Invalid signature" });
                }

                // 3. 解析消息
                //    对 TikTok 平台使用扩展解析（含事件类型），其他平台使用标准解析
                WebhookEventType eventType;
                PlatformMessage platformMsg;
                if (client is TikTokShopPlatformClient tiktokClient)
                {
                    var result = await tiktokClient.ParseWebhookResultAsync(Request);
                    platformMsg = result.Message;
                    eventType = result.EventType;
                }
                else
                {
                    platformMsg = await client.ParseWebhookAsync(Request);
                    // Shopee 等：ParseWebhook 对买家聊天会填 CustomerId；无买家则视为非 IM
                    eventType = !string.IsNullOrEmpty(platformMsg.CustomerId)
                        ? WebhookEventType.IM_MESSAGE_RECEIVED
                        : WebhookEventType.Unknown;
                }

                _logger.LogInformation("[Webhook] Parsed message: Platform={Platform}, Event={Event}, MsgId={MsgId}, Content={Content}",
                    platformMsg.Platform, eventType, platformMsg.MsgId, platformMsg.Content);

                // 4. 幂等：ProcessedWebhookEvent 唯一键 try-insert，冲突即 duplicate
                if (!await TryClaimWebhookEventAsync(platformMsg))
                {
                    _logger.LogWarning("[Webhook] Duplicate webhook ignored: MsgId={MsgId}", platformMsg.MsgId);
                    return Ok(new { code = 0, message = "duplicate" });
                }

                // 5. 根据事件类型路由处理
                switch (eventType)
                {
                    case WebhookEventType.IM_MESSAGE_RECEIVED:
                        return await HandleChatMessageAsync(platformMsg);

                    case WebhookEventType.ORDER_PAYMENT:
                    case WebhookEventType.ORDER_CANCELLED:
                    case WebhookEventType.ORDER_SHIPPED:
                    case WebhookEventType.ORDER_COMPLETED:
                    case WebhookEventType.ORDER_CREATED:
                        return await HandleOrderEventAsync(platformMsg, eventType);

                    default:
                        _logger.LogWarning("[Webhook] Unhandled event type: {EventType} for {Platform}",
                            eventType, platform);
                        return Ok(new { code = 0, message = "event_type_not_handled", event_type = eventType });
                }
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning("[Webhook] Unsupported platform: {Platform} - {Message}", platform, ex.Message);
                return NotFound(new { code = 404, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Webhook] Processing failed for {Platform} after {Elapsed}ms",
                    platform, sw.ElapsedMilliseconds);
                return StatusCode(500, new { code = 500, message = "server error" });
            }
        }

        /// <summary>
        /// 🔥 处理聊天消息（IM）
        /// </summary>
        private async Task<IActionResult> HandleChatMessageAsync(PlatformMessage msg)
        {
            if (string.IsNullOrWhiteSpace(msg.Content))
            {
                _logger.LogWarning("[Webhook] Chat message with empty content ignored");
                return Ok(new { code = 0, message = "empty_content_ignored" });
            }

            // 查找或创建会话
            var session = await FindOrCreateSessionAsync(msg);
            if (session == null)
            {
                _logger.LogWarning("[Webhook] Session creation failed for {Platform} Customer={Customer}",
                    msg.Platform, msg.CustomerId);
                // 仍然返回 200（避免平台重试），但不处理
                return Ok(new { code = 0, message = "session_not_found" });
            }

            // 刷新平台回复上下文（人审发送时需要 conversation_id / shop_id）
            session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);

            // 添加用户消息到数据库
            var userMsg = ChatMessage.FromUser(msg.Content, session.Id);
            userMsg.PlatformMsgId = msg.MsgId;  // 设置平台消息ID用于去重
            session.Messages.Add(userMsg);
            session.AddUserMessage();
            await _db.SaveChangesAsync();

            // 意图分类 → Agent 路由 → 平台回信（独立 scope，不阻塞 webhook 200）
            var sessionId = session.Id;
            var platformSnapshot = msg;
            var userContent = msg.Content;
            _ = Task.Run(async () =>
            {
                await ProcessInboundAiReplyAsync(sessionId, platformSnapshot, userContent);
            });

            return Ok(new { code = 0, message = "success" });
        }

        /// <summary>
        /// 🔥 处理订单事件
        /// </summary>
        private async Task<IActionResult> HandleOrderEventAsync(PlatformMessage msg, WebhookEventType eventType)
        {
            _logger.LogInformation("[Webhook] Order event: {Event} Order={Order}", eventType, msg.CustomerId);

            // 记录到数据库（可作为后续订单处理的数据源）
            var orderEvent = new
            {
                event_type = eventType.ToString(),
                order_id = msg.CustomerId,
                shop_id = msg.OpenId,
                content = msg.Content,
                received_at = DateTime.UtcNow
            };

            _logger.LogInformation("[Webhook] Order event payload: {Payload}", JsonSerializer.Serialize(orderEvent));

            // 异步通知商户（如站内信、邮件、钉钉机器人）
            _ = Task.Run(async () =>
            {
                try
                {
                    // TODO: 集成通知服务（邮件/钉钉/企业微信）
                    _logger.LogInformation("[Webhook] Order event notification sent: {Event} Order={Order}",
                        eventType, msg.CustomerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Webhook] Order event notification failed for {Event}", eventType);
                }
            });

            return Ok(new { code = 0, message = "order_event_received", event_type = eventType.ToString() });
        }

        #endregion

        #region === TikTok 专用测试端点 ===

        /// <summary>
        /// 🔥 TikTok 模拟 Webhook 测试端点
        /// 支持三种测试模式: chat (聊天), order (订单), signature (签名验证)
        /// </summary>
        [HttpPost("tiktok/test")]
        public IActionResult TestTikTok([FromBody] TikTokTestRequest request)
        {
            try
            {
                switch (request.Mode)
                {
                    case "chat":
                        return HandleTikTokChatTest(request);

                    case "order":
                        return HandleTikTokOrderTest(request);

                    case "signature":
                        return HandleTikTokSignatureTest(request);

                    default:
                        return BadRequest(new { code = 400, message = $"Unknown mode: {request.Mode}. Use: chat, order, signature" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok-Test] Test failed");
                return StatusCode(500, new { code = 500, message = ex.Message });
            }
        }

        private IActionResult HandleTikTokChatTest(TikTokTestRequest request)
        {
            _logger.LogInformation("[TikTok-Test] Chat test: Customer={Customer}, Message={Message}",
                request.CustomerId, request.Message);

            var seller = Seller.Create(
                openId: request.SellerOpenId ?? Guid.NewGuid().ToString(),
                nickname: request.SellerName ?? "TikTok Shop"
            );
            _db.Sellers.Add(seller);

            var session = ChatSession.Create(
                shopId: seller.Id,
                platform: "TIKTOK",
                customerId: request.CustomerId,
                customerName: request.CustomerName
            );
            _db.ChatSessions.Add(session);

            var userMsg = ChatMessage.FromUser(request.Message, session.Id);
            session.Messages.Add(userMsg);
            session.AddUserMessage();

            var aiReply = $"【TikTok AI 自动回复】您好！您关于「{request.Message}」的问题已收到，我们会尽快处理。";
            var aiMsg = ChatMessage.FromAI(aiReply, chatSessionId: session.Id);
            session.Messages.Add(aiMsg);
            session.AddAiMessage();

            _db.SaveChangesAsync();

            return Ok(new {
                code = 0,
                message = "success",
                reply = aiReply,
                session_id = session.Id,
                session_no = session.SessionId
            });
        }

        private IActionResult HandleTikTokOrderTest(TikTokTestRequest request)
        {
            _logger.LogInformation("[TikTok-Test] Order test: Event={Event}, Order={Order}",
                request.EventType, request.OrderId);

            return Ok(new {
                code = 0,
                message = $"Order event {request.EventType} simulated",
                order_id = request.OrderId,
                event_type = request.EventType,
                timestamp = DateTime.UtcNow
            });
        }

        private IActionResult HandleTikTokSignatureTest(TikTokTestRequest request)
        {
            // 签名验证测试：使用硬编码密钥（测试用），生产环境应从配置读取
            var appSecret = "test-secret";
            var baseString = $"{request.Timestamp}\n{request.Body}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var computed = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString)))
                .Replace("-", "").ToLowerInvariant();

            var isValid = computed == (request.Signature ?? "").ToLowerInvariant();

            return Ok(new {
                code = 0,
                message = isValid ? "signature_valid" : "signature_mismatch",
                computed_signature = computed,
                received_signature = request.Signature,
                is_valid = isValid
            });
        }

        #endregion

        #region === Shopee 测试端点（保持不变） ===

        [HttpPost("shopee/test")]
        public async Task<IActionResult> TestShopee([FromBody] TestShopeeMessage request)
        {
            try
            {
                _logger.LogInformation("[Shopee] Test webhook received: Customer={Customer}, Message={Message}",
                    request.CustomerId, request.Message);

                var seller = Seller.Create(
                    openId: Guid.NewGuid().ToString(),
                    nickname: request.CustomerName ?? "Mock Shop"
                );
                _db.Sellers.Add(seller);

                var session = ChatSession.Create(
                    shopId: seller.Id,
                    platform: "SHOPEE",
                    customerId: request.CustomerId,
                    customerName: request.CustomerName
                );
                _db.ChatSessions.Add(session);

                await _db.SaveChangesAsync();

                var userMsg = ChatMessage.FromUser(request.Message, session.Id);
                session.Messages.Add(userMsg);
                session.AddUserMessage();

                var aiReply = "【模拟自动回复】您的消息已收到，我们会尽快处理。";

                var aiMsg = ChatMessage.FromAI(aiReply, chatSessionId: session.Id);
                session.Messages.Add(aiMsg);
                session.AddAiMessage();

                await _db.SaveChangesAsync();

                _logger.LogInformation("[Shopee] Test reply generated: {Reply}", aiReply);

                return Ok(new { code = 0, message = "success", reply = aiReply });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] Test webhook failed");
                return StatusCode(500, new { code = 500, message = ex.Message });
            }
        }

        #endregion

        #region === 私有辅助方法 ===

        /// <summary>
        /// 幂等认领：EventKey=MsgId；空 MsgId 则弱键 hash(platform+customerId+content+timestamp bucket) 并记日志。
        /// try-insert 唯一键成功返回 true（可继续处理）；冲突返回 false（duplicate）。
        /// </summary>
        private async Task<bool> TryClaimWebhookEventAsync(PlatformMessage msg)
        {
            var platform = (msg.Platform ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(platform))
                platform = "UNKNOWN";

            string eventKey;
            var isWeak = false;
            if (!string.IsNullOrWhiteSpace(msg.MsgId))
            {
                eventKey = msg.MsgId.Trim();
            }
            else
            {
                isWeak = true;
                // 60 秒时间桶，弱防抖；空 MsgId 仍可能漏检，仅作兜底
                var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
                var raw = $"{platform}|{msg.CustomerId}|{msg.Content}|{bucket}";
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                eventKey = "weak:" + Convert.ToHexString(hash).ToLowerInvariant();
                _logger.LogWarning(
                    "[Webhook] Empty MsgId — using weak EventKey for platform={Platform} customer={Customer}",
                    platform, msg.CustomerId);
            }

            if (eventKey.Length > 191)
                eventKey = eventKey[..191];

            try
            {
                _db.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
                {
                    Id = Guid.NewGuid(),
                    Platform = platform.Length > 32 ? platform[..32] : platform,
                    EventKey = eventKey,
                    ProcessedAt = DateTime.UtcNow,
                    IsWeakKey = isWeak
                });
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                _logger.LogInformation(
                    "[Webhook] Idempotent claim conflict Platform={Platform} EventKey={EventKey}",
                    platform, eventKey);
                // 清掉跟踪失败的实体，避免污染后续 SaveChanges
                foreach (var entry in _db.ChangeTracker.Entries<ProcessedWebhookEvent>()
                             .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                             .ToList())
                {
                    entry.State = EntityState.Detached;
                }
                return false;
            }
            catch (Exception ex)
            {
                // 表未就绪等：回退到 ChatMessage.PlatformMsgId（尽力），仍放行以免丢消息
                _logger.LogWarning(ex, "[Webhook] Idempotency claim failed; falling back to PlatformMsgId check");
                if (!string.IsNullOrEmpty(msg.MsgId))
                {
                    try
                    {
                        var existed = await _db.Set<ChatMessage>()
                            .AnyAsync(m => m.PlatformMsgId == msg.MsgId);
                        if (existed) return false;
                    }
                    catch { /* ignore */ }
                }
                return true;
            }
        }

        /// <summary>
        /// 查找或创建会话
        /// </summary>
        private async Task<ChatSession?> FindOrCreateSessionAsync(PlatformMessage msg)
        {
            // 通过 CustomerId + Platform 查找已有会话
            var existing = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Platform == msg.Platform && s.CustomerId == msg.CustomerId);

            if (existing != null) return existing;

            // 创建新会话：按平台店铺 ID 找 Seller（ShopId 或 OpenId 均可能存 to_shop_id）
            var platformConn = await _db.Set<PlatformConnection>()
                .FirstOrDefaultAsync(pc =>
                    pc.Platform == msg.Platform &&
                    pc.IsActive &&
                    (pc.ShopId == msg.OpenId || pc.OpenId == msg.OpenId));

            if (platformConn != null)
            {
                var seller = await _db.Sellers.FindAsync(platformConn.SellerId);
                if (seller != null)
                {
                    var newSession = ChatSession.Create(
                        shopId: seller.Id,
                        platform: msg.Platform,
                        customerId: msg.CustomerId,
                        customerName: msg.CustomerName
                    );
                    _db.ChatSessions.Add(newSession);
                    await _db.SaveChangesAsync();
                    return newSession;
                }
            }

            // 兜底：创建临时 Seller（演示模式）
            _logger.LogWarning("[Webhook] No seller found for platform={Platform}, creating temporary seller", msg.Platform);
            var tempSeller = Seller.Create(
                openId: msg.OpenId ?? Guid.NewGuid().ToString(),
                nickname: $"{msg.Platform} Shop"
            );
            _db.Sellers.Add(tempSeller);
            await _db.SaveChangesAsync();

            var fallbackSession = ChatSession.Create(
                shopId: tempSeller.Id,
                platform: msg.Platform,
                customerId: msg.CustomerId,
                customerName: msg.CustomerName
            );
            _db.ChatSessions.Add(fallbackSession);
            await _db.SaveChangesAsync();

            return fallbackSession;
        }

        /// <summary>
        /// 已落库的用户消息 → IntentClassifier → IAgent / GeneralChat → 持久化为草稿（默认）。
        /// 仅当 SellerConfig.OutboundMode=AutoSend 时才调用平台 SendReplyAsync。
        /// 缺 AI Key / 平台 Token 时仅打日志，不向上抛（Webhook 已返回 200）。
        /// </summary>
        private async Task ProcessInboundAiReplyAsync(Guid sessionId, PlatformMessage msg, string userContent)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                var db = sp.GetRequiredService<AppDbContext>();
                var logger = sp.GetRequiredService<ILogger<WebhookController>>();

                var session = await db.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);
                if (session == null)
                {
                    logger.LogWarning("[Webhook] Session {SessionId} missing for AI reply", sessionId);
                    return;
                }

                // EnableAutoReply=false：不生成草稿；默认仍生成 AI 草稿（DraftFirst）
                var sellerConfig = await db.SellerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == session.ShopId);
                if (sellerConfig != null && !sellerConfig.EnableAutoReply)
                {
                    logger.LogInformation(
                        "[Webhook] AI draft/reply disabled for shop {ShopId}; skipping",
                        session.ShopId);
                    return;
                }

                // 转人工硬闸：PendingHumanHandoff 时停止生成新草稿与 AutoSend（旧草稿保留可见）
                // 注意：新建会话 Status 也是 Pending，不能单靠 Status 判断
                if (session.BlocksAiDrafting)
                {
                    logger.LogInformation(
                        "[Webhook] Handoff hard-gate: skip AI draft/AutoSend Session={SessionId} Shop={ShopId}",
                        sessionId, session.ShopId);
                    session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);
                    await db.SaveChangesAsync();
                    return;
                }

                session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);

                // —— 敏感词自动 handoff：命中则转人工，不生成新草稿 ——
                var sensitiveHit = OutboundPolicyHelper.FindSensitiveHit(
                    userContent, sellerConfig?.SensitiveKeywords);
                if (sensitiveHit != null)
                {
                    session.TransferToAgent();
                    await SupersedePendingDraftsAsync(db, session.Id);
                    await db.SaveChangesAsync();
                    logger.LogWarning(
                        "[Webhook] Auto-handoff by sensitive keyword '{Keyword}' Session={SessionId} Shop={ShopId}",
                        sensitiveHit, sessionId, session.ShopId);
                    return;
                }

                // —— 营业时间外：禁止 AutoSend；可系统提示草稿；默认直接 handoff ——
                var withinHours = OutboundPolicyHelper.IsWithinBusinessHours(
                    sellerConfig?.BusinessHoursStart,
                    sellerConfig?.BusinessHoursEnd,
                    sellerConfig?.TimeZoneId);
                var handoffOutside = sellerConfig?.HandoffOutsideBusinessHours ?? true;
                if (!withinHours)
                {
                    logger.LogInformation(
                        "[Webhook] Outside business hours Shop={ShopId} Session={SessionId} Hours={Start}-{End} Tz={Tz}",
                        session.ShopId, sessionId,
                        sellerConfig?.BusinessHoursStart ?? "09:00",
                        sellerConfig?.BusinessHoursEnd ?? "22:00",
                        sellerConfig?.TimeZoneId ?? "Asia/Shanghai");

                    if (handoffOutside)
                    {
                        session.TransferToAgent();
                        await SupersedePendingDraftsAsync(db, session.Id);
                        var tip = new DraftMessage
                        {
                            Id = Guid.NewGuid(),
                            ChatSessionId = session.Id,
                            Content = "【营业外】非营业时间，请人工稍后回复。",
                            Status = DraftStatuses.Pending,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.DraftMessages.Add(tip);
                        await db.SaveChangesAsync();
                        logger.LogInformation(
                            "[Webhook] Outside-hours handoff Session={SessionId}; tip draft saved (no AI / no AutoSend)",
                            sessionId);
                        return;
                    }
                }

                var outboundMode = OutboundModes.Normalize(sellerConfig?.OutboundMode);
                var allowAutoSend = OutboundModes.IsAutoSend(outboundMode)
                    && !session.PendingHumanHandoff
                    && session.Status != SessionStatus.Pending
                    && withinHours; // 营业外永不 AutoSend

                var historyDtos = session.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageDto
                    {
                        IsFromUser = m.SenderType == 1,
                        Content = m.Content,
                        MessageType = m.MessageType == 1 ? "text" : "other",
                        Timestamp = m.CreatedAt
                    })
                    .ToList();

                var chatContext = new ChatContext
                {
                    ConversationId = session.Id.ToString(),
                    ShopId = session.ShopId,
                    SellerId = session.ShopId.ToString(),
                    Platform = session.Platform,
                    CustomerId = session.CustomerId ?? msg.CustomerId ?? string.Empty,
                    // Shopee ParseWebhook 把 to_shop_id 放在 OpenId，供 OrderAgent / SendReply 按店取 token
                    PlatformShopId = msg.OpenId,
                    Messages = historyDtos
                };

                ChatIntent intent = ChatIntent.Unknown;
                double confidence = 0.15;
                try
                {
                    var classifier = sp.GetRequiredService<IIntentClassifier>();
                    var classified = await classifier.ClassifyWithConfidenceAsync(userContent, historyDtos, session.ShopId, session.Id);
                    intent = classified.Intent;
                    confidence = classified.Confidence;
                    logger.LogInformation(
                        "[Webhook] Intent={Intent} Confidence={Confidence:F2} Session={SessionId} Platform={Platform}",
                        intent, confidence, sessionId, session.Platform);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Webhook] Intent classify failed (missing AI key?); fallback Unknown");
                    intent = ChatIntent.Unknown;
                    confidence = 0.15;
                }

                // —— 低置信度自动 handoff ——
                var autoLow = sellerConfig?.AutoHandoffOnLowConfidence ?? true;
                var threshold = sellerConfig?.HandoffConfidenceThreshold ?? 0.45;
                if (threshold < 0) threshold = 0;
                if (threshold > 1) threshold = 1;
                if (autoLow && confidence < threshold)
                {
                    session.TransferToAgent();
                    await SupersedePendingDraftsAsync(db, session.Id);
                    await db.SaveChangesAsync();
                    logger.LogWarning(
                        "[Webhook] Auto-handoff by low confidence {Confidence:F2}<{Threshold:F2} Intent={Intent} Session={SessionId} Shop={ShopId}",
                        confidence, threshold, intent, sessionId, session.ShopId);
                    return;
                }

                string replyContent;
                try
                {
                    var agentRouter = sp.GetRequiredService<IAgentRouter>();
                    var agent = agentRouter.GetAgent(intent);

                    // 有匹配 Agent（如 OrderQuery→OrderAgent）则走专用路径；General/Unknown 走口语化 LLM
                    if (agent != null && intent is not (ChatIntent.GeneralChat or ChatIntent.Unknown))
                    {
                        var agentResult = await agent.ProcessAsync(userContent, chatContext);
                        replyContent = agentResult.Messages.LastOrDefault()?.Content
                            ?? "抱歉，我暂时无法处理您的问题。";
                    }
                    else
                    {
                        var generalChat = sp.GetRequiredService<IGeneralChatAgent>();
                        replyContent = await generalChat.GenerateReplyAsync(userContent, chatContext);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Webhook] Agent/LLM reply failed; using local fallback");
                    replyContent = "您好！您的消息已收到，我们会尽快为您处理。";
                }

                if (!withinHours)
                    replyContent = "【营业外】" + replyContent;

                await SupersedePendingDraftsAsync(db, session.Id);

                if (allowAutoSend)
                {
                    // 合规风险路径：显式 AutoSend 才直接出站
                    var aiMsg = ChatMessage.FromAI(replyContent, chatSessionId: session.Id);
                    session.Messages.Add(aiMsg);
                    session.AddAiMessage();
                    await db.SaveChangesAsync();

                    try
                    {
                        var platformRouter = sp.GetRequiredService<IPlatformClientRouter>();
                        var client = platformRouter.GetClient(msg.Platform);
                        var platformOutboundId = await client.SendReplyAsync(msg, replyContent);
                        // 优先平台真实 message_id；无则 outbound:{localId} 兜底
                        aiMsg.PlatformMsgId = !string.IsNullOrWhiteSpace(platformOutboundId)
                            ? platformOutboundId
                            : $"outbound:{aiMsg.Id:N}";
                        await db.SaveChangesAsync();
                        logger.LogWarning(
                            "[Webhook] AutoSend used for shop {ShopId} platform {Platform} — compliance risk; prefer DraftFirst",
                            session.ShopId, msg.Platform);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "[Webhook] SendReply skipped/failed for {Platform} (missing token/config?)",
                            msg.Platform);
                    }
                }
                else
                {
                    // 默认 DraftFirst：只落草稿，不调用平台 SendReply
                    var draft = new DraftMessage
                    {
                        Id = Guid.NewGuid(),
                        ChatSessionId = session.Id,
                        Content = replyContent,
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.DraftMessages.Add(draft);
                    await db.SaveChangesAsync();
                    logger.LogInformation(
                        "[Webhook] Draft saved Session={SessionId} DraftId={DraftId} Mode={Mode} OutsideHours={Outside} (no SendReply)",
                        sessionId, draft.Id, outboundMode, !withinHours);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Webhook] Async reply failed for session {SessionId}", sessionId);
            }
        }

        private static async Task SupersedePendingDraftsAsync(AppDbContext db, Guid sessionId)
        {
            var oldDrafts = await db.DraftMessages
                .Where(d => d.ChatSessionId == sessionId && d.Status == DraftStatuses.Pending)
                .ToListAsync();
            foreach (var d in oldDrafts)
            {
                d.Status = DraftStatuses.Superseded;
                d.UpdatedAt = DateTime.UtcNow;
            }
        }


        #endregion
    }

    #region === DTOs ===

    /// <summary>
    /// TikTok 测试请求体
    /// </summary>
    public class TikTokTestRequest
    {
        public string Mode { get; set; } = "chat";     // chat / order / signature
        public string CustomerId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string SellerOpenId { get; set; } = string.Empty;
        public string? SellerName { get; set; }

        // chat 模式
        public string Message { get; set; } = string.Empty;

        // order 模式
        public string EventType { get; set; } = "ORDER_PAYMENT";
        public string OrderId { get; set; } = string.Empty;

        // signature 模式
        public string Timestamp { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Signature { get; set; }
    }

    /// <summary>
    /// 模拟 Shopee 消息请求体
    /// </summary>
    public class TestShopeeMessage
    {
        public string CustomerId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}
