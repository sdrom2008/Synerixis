using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System.Text.Json;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 平台 Webhook 接收控制器
    /// </summary>
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly IPlatformClient _platformClient;
        private readonly IConversationRepository _conversationRepo;
        private readonly ILogger<WebhookController> _logger;
        private readonly AppDbContext _db;

        public WebhookController(
            IPlatformClient platformClient,
            IConversationRepository conversationRepo,
            AppDbContext db,
            ILogger<WebhookController> logger)
        {
            _platformClient = platformClient;
            _conversationRepo = conversationRepo;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 淘宝消息推送（测试用）
        /// </summary>
        [HttpPost("taobao")]
        public async Task<IActionResult> Taobao()
        {
            try
            {
                // 1. 验证签名（暂时跳过）
                // if (!await _platformClient.VerifySignatureAsync(Request)) return BadRequest("Invalid signature");

                // 2. 解析消息
                var platformMsg = await _platformClient.ParseWebhookAsync(Request);

                _logger.LogInformation("[Taobao] Received message from {OpenId}: {Content}", platformMsg.OpenId, platformMsg.Content);

                // 3. 找到或创建 ChatSession
                var session = await FindOrCreateSessionAsync(platformMsg);
                if (session == null) return BadRequest("Session not found or create failed");

                // 4. 添加用户消息（买家）
                var userMsg = ChatMessage.FromUser(platformMsg.Content, session.Id);
                session.Messages.Add(userMsg);
                session.AddUserMessage();

                // 5. 调用 AI 自动回复
                var historyDtos = session.Messages.Select(m => new
                {
                    isFromUser = m.SenderType == 1,
                    content = m.Content
                }).ToList();

                // TODO: 调用 AI 服务（需要注入 IIntentClassifier + IGeneralChatAgent）
                // var aiReply = await _aiChatService.GenerateReplyAsync(session, historyDtos);
                // var aiMsg = ChatMessage.FromAI(aiReply, session.Id);
                // session.Messages.Add(aiMsg);
                // session.AddAiMessage();

                // 暂时只保存用户消息，不回复
                await _db.SaveChangesAsync();

                // 6. 返回成功（淘宝要求 200 OK）
                return Ok(new { code = 0, message = "success" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Taobao] Webhook processing failed");
                return StatusCode(500, new { code = 500, message = "server error" });
            }
        }

        private async Task<ChatSession?> FindOrCreateSessionAsync(PlatformMessage msg)
        {
            // 通过 OpenId + Platform 查找已有会话
            var existing = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Platform == "TAOBAO" && s.CustomerId == msg.OpenId);

            if (existing != null) return existing;

            // 创建新会话：需要知道 ShopId（从 PlatformConnection 里查）
            var platformConn = await _db.Set<PlatformConnection>()
                .FirstOrDefaultAsync(pc => pc.Platform == "TAOBAO" && pc.OpenId == msg.OpenId);

            if (platformConn == null)
            {
                _logger.LogWarning("[Taobao] No PlatformConnection found for OpenId={OpenId}", msg.OpenId);
                return null;
            }

            if (string.IsNullOrEmpty(platformConn.ShopId))
            {
                _logger.LogError("[Taobao] PlatformConnection ShopId is empty for OpenId={OpenId}", msg.OpenId);
                return null;
            }

            var shopId = Guid.Parse(platformConn.ShopId);
            var newSession = ChatSession.Create(
                shopId: shopId,
                platform: "TAOBAO",
                customerId: msg.OpenId,
                customerName: msg.CustomerName ?? "淘宝客户"
            );
            _db.ChatSessions.Add(newSession);
            await _db.SaveChangesAsync();

            return newSession;
        }
    }
}
