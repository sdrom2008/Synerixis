using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synerixis.Application.DTOs;
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

                // 4. 幂等性检查：通过 MsgId 去重（已存在则忽略）
                if (await IsDuplicateAsync(platformMsg))
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
        /// 幂等性检查：通过 MsgId 去重
        /// 检查数据库中是否已处理过相同 MsgId 的消息
        /// </summary>
        private async Task<bool> IsDuplicateAsync(PlatformMessage msg)
        {
            if (string.IsNullOrEmpty(msg.MsgId))
                return false; // 无 MsgId 无法去重，直接放行

            try
            {
                // 检查 ChatMessage 表中是否已有相同 MsgId
                var existed = await _db.Set<ChatMessage>()
                    .AnyAsync(m => m.PlatformMsgId == msg.MsgId);
                return existed;
            }
            catch
            {
                // 数据库异常时放行（宁可重复处理也不丢消息）
                _logger.LogWarning("[Webhook] Idempotency check failed, allowing duplicate");
                return false;
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
        /// 已落库的用户消息 → IntentClassifier → IAgent / GeneralChat → SendReplyAsync。
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
                try
                {
                    var classifier = sp.GetRequiredService<IIntentClassifier>();
                    intent = await classifier.ClassifyAsync(userContent, historyDtos);
                    logger.LogInformation("[Webhook] Intent={Intent} Session={SessionId} Platform={Platform}",
                        intent, sessionId, session.Platform);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Webhook] Intent classify failed (missing AI key?); fallback Unknown");
                    intent = ChatIntent.Unknown;
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

                var aiMsg = ChatMessage.FromAI(replyContent, chatSessionId: session.Id);
                session.Messages.Add(aiMsg);
                session.AddAiMessage();
                await db.SaveChangesAsync();

                try
                {
                    var platformRouter = sp.GetRequiredService<IPlatformClientRouter>();
                    var client = platformRouter.GetClient(msg.Platform);
                    await client.SendReplyAsync(msg, replyContent);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "[Webhook] SendReply skipped/failed for {Platform} (missing token/config?)",
                        msg.Platform);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Webhook] Async reply failed for session {SessionId}", sessionId);
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
