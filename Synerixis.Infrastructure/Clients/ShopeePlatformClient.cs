using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Web;

namespace Synerixis.Infrastructure.Clients
{
    /// <summary>
    /// Shopee 平台客户端（待补充：签名验证、消息解密、发送回复的具体逻辑）
    /// </summary>
    public class ShopeePlatformClient : IPlatformClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ShopeePlatformClient> _logger;

        public ShopeePlatformClient(IConfiguration config, ILogger<ShopeePlatformClient> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// 发送回复给Shopee买家（待实现）
        /// </summary>
        public async Task SendReplyAsync(string openId, string content, CancellationToken cancellationToken = default)
        {
            // TODO: 调用Shopee聊天消息发送API
            // 需要：access_token、shop_id、user_id (openId)、content
            // API 地址：https://open.shopee.com/documents/v2/v2.chat.send_message?module=...&type=...
            _logger.LogWarning("[Shopee] SendReplyAsync not implemented yet. openId={OpenId}, content={Content}", openId, content);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 解析 Webhook 请求（占位）
        /// </summary>
        public async Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            // TODO: 读取请求体，验证签名，解析 JSON
            // Shopee Webhook 推送包含 headers: Authorization, X-Shopee-Request-Id, content-type: application/json
            // 签名在 Authorization: Bearer <signature>
            _logger.LogWarning("[Shopee] ParseWebhookAsync not implemented yet.");
            using var reader = new StreamReader(request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            _logger.LogDebug("[Shopee] Webhook body: {Body}", body);

            // 返回占位消息（后续解析真实数据）
            return new PlatformMessage
            {
                Platform = "SHOPEE",
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
            // TODO: 从 Header Authorization 读取 signature，按 Shopee 规则计算并比对
            // 签名算法：HMAC-SHA256 使用 AppSecret
            _logger.LogWarning("[Shopee] VerifySignatureAsync not implemented yet.");
            await Task.CompletedTask;
            return true; // 临时绕过
        }
    }
}
