using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Synerixis.Infrastructure.Clients
{
    /// <summary>
    /// 淘宝平台客户端（占位实现）
    /// 待补充：签名验证、消息解密、发送回复的具体逻辑
    /// </summary>
    public class TaobaoPlatformClient : IPlatformClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TaobaoPlatformClient> _logger;

        public TaobaoPlatformClient(IConfiguration config, ILogger<TaobaoPlatformClient> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// 发送回复给淘宝买家（待实现）
        /// </summary>
        public async Task SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default)
        {
            // TODO: 调用淘宝消息发送API
            // context: PlatformMessage 包含 OpenId (buyer open_id), CustomerId, ConversationId等
            // 需要：access_token、open_id、content
            // API 地址：https://eco.taobao.com/router/rest
            _logger.LogWarning("[Taobao] SendReplyAsync not implemented yet. OpenId={OpenId}, Content={Content}, ConversationId={ConvId}", 
                context.OpenId, content, context.ConversationId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 解析 Webhook 请求（占位）
        /// </summary>
        public async Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: 读取请求体，验证签名，解密（如果需要），解析 JSON
            // 淘宝 Webhook 消息结构包含：open_id、buyer_nick、content、msg_id、create_time 等
            _logger.LogWarning("[Taobao] ParseWebhookAsync not implemented yet.");
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            _logger.LogDebug("[Taobao] Webhook body: {Body}", body);

            // 返回占位消息
            return new PlatformMessage
            {
                Platform = "TAOBAO",
                OpenId = "unknown",
                Content = body,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 验证 Webhook 签名（占位）
        /// </summary>
        public async Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: 从 Header 读取签名（如 X-Signature），按淘宝规则计算并比对
            _logger.LogWarning("[Taobao] VerifySignatureAsync not implemented yet.");
            await Task.CompletedTask;
            return true; // 临时绕过
        }
    }
}
