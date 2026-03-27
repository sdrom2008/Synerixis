using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Clients;
using Synerixis.Infrastructure.Data;
using System.Text.Json;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 通用平台 Webhook 接收控制器
    /// </summary>
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly PlatformClientRouter _router;
        private readonly IConversationRepository _conversationRepo;
        private readonly ILogger<WebhookController> _logger;
        private readonly AppDbContext _db;

        public WebhookController(
            PlatformClientRouter router,
            IConversationRepository conversationRepo,
            AppDbContext db,
            ILogger<WebhookController> logger)
        {
            _router = router;
            _conversationRepo = conversationRepo;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 接收平台消息推送（淘宝、抖店、Shopee等）
        /// </summary>
        [HttpPost("{platform}")]
        public async Task<IActionResult> Handle(string platform)
        {
            try
            {
                _logger.LogInformation("[{Platform}] Received webhook from {RemoteIp}", platform, HttpContext.Connection.RemoteIpAddress);

                // 获取对应平台的客户端
                var client = _router.GetClient(platform);

                // 1. 验证签名
                if (!await client.VerifySignatureAsync(Request))
                {
                    _logger.LogWarning("[{Platform}] Signature verification failed", platform);
                    return BadRequest(new { code = 401, message = "Invalid signature" });
                }

                // 2. 解析消息
                var platformMsg = await client.ParseWebhookAsync(Request);
                _logger.LogInformation("[{Platform}] Received message from {OpenId}: {Content}", platform, platformMsg.OpenId, platformMsg.Content);

                // 3. 查找或创建会话
                var session = await FindOrCreateSessionAsync(platformMsg);
                if (session == null)
                {
                    _logger.LogWarning("[{Platform}] Session not found or create failed for OpenId={OpenId}", platform, platformMsg.OpenId);
                    return BadRequest(new { code = 404, message = "Session not found" });
                }

                // 4. 添加用户消息
                var userMsg = ChatMessage.FromUser(platformMsg.Content, session.Id);
                session.Messages.Add(userMsg);
                session.AddUserMessage();

                // 5. 🔥 调用 AI 自动回复（新接入）- 演示用模拟回复
                // TODO: 接入真实 AI 服务
                var aiReply = "这是一个模拟的 AI 自动回复。实际部署后将连接阿里云通义千问。";

                var aiMsg = ChatMessage.FromAI(aiReply, chatSessionId: session.Id);
                session.Messages.Add(aiMsg);
                session.AddAiMessage();

                await _db.SaveChangesAsync();

                // 6. 调用平台发送回复（异步，不阻塞响应）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await client.SendReplyAsync(platformMsg, aiReply);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[{Platform}] Failed to send reply to {OpenId}", platform, platformMsg.OpenId);
                    }
                });

                // 7. 返回成功（各平台要求 200 OK）
                return Ok(new { code = 0, message = "success", reply = aiReply });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Platform}] Webhook processing failed", platform);
                return StatusCode(500, new { code = 500, message = "server error" });
            }
        }

        /// <summary>
        /// 🔥 模拟Shopee消息推送（用于演示和测试）- 不需要真实平台
        /// </summary>
        [HttpPost("shopee/test")]
        public async Task<IActionResult> TestShopee([FromBody] TestShopeeMessage request)
        {
            try
            {
                _logger.LogInformation("[Shopee] Test webhook received: Customer={Customer}, Message={Message}", request.CustomerId, request.Message);

                // 创建 Seller（作为店铺主体）
                var seller = Seller.Create(
                    openId: Guid.NewGuid().ToString(),
                    nickname: request.CustomerName ?? "Mock Shop"
                );
                _db.Sellers.Add(seller);

                // 创建会话（使用 seller.Id 作为外键）
                var session = ChatSession.Create(
                    shopId: seller.Id,
                    platform: "SHOPEE",
                    customerId: request.CustomerId,
                    customerName: request.CustomerName
                );
                _db.ChatSessions.Add(session);

                await _db.SaveChangesAsync();

                // 添加用户消息
                var userMsg = ChatMessage.FromUser(request.Message, session.Id);
                session.Messages.Add(userMsg);
                session.AddUserMessage();

                // 模拟 AI 自动回复
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

        private async Task<ChatSession?> FindOrCreateSessionAsync(PlatformMessage msg)
        {
            // 通过 OpenId + Platform 查找已有会话
            var existing = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Platform == msg.Platform && s.CustomerId == msg.OpenId);

            if (existing != null) return existing;

            // 创建新会话：需要从 PlatformConnection 获取 ShopId
            var platformConn = await _db.Set<PlatformConnection>()
                .FirstOrDefaultAsync(pc => pc.Platform == msg.Platform && pc.OpenId == msg.OpenId);

            if (platformConn == null)
            {
                _logger.LogWarning("[{Platform}] No PlatformConnection found for OpenId={OpenId}", msg.Platform, msg.OpenId);
                return null;
            }

            if (string.IsNullOrEmpty(platformConn.ShopId))
            {
                _logger.LogError("[{Platform}] PlatformConnection ShopId is empty for OpenId={OpenId}", msg.Platform, msg.OpenId);
                return null;
            }

            var shopId = Guid.Parse(platformConn.ShopId);
            var newSession = ChatSession.Create(
                shopId: shopId,
                platform: msg.Platform,
                customerId: msg.OpenId,
                customerName: msg.CustomerName ?? $"{msg.Platform}客户"
            );
            _db.ChatSessions.Add(newSession);
            await _db.SaveChangesAsync();

            return newSession;
        }
    }

    /// <summary>
    /// 模拟Shopee消息请求体
    /// </summary>
    public class TestShopeeMessage
    {
        public string CustomerId { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
