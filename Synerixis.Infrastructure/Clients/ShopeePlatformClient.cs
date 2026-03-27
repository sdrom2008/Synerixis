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
        /// 验证 Webhook 签名（Shopee 使用 HMAC-SHA256，签名位于 Authorization 头，格式 "Bearer <signature>"）
        /// </summary>
        public async Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // 开启请求体缓冲，以便多次读取（VerifySignature 会用一次，ParseWebhook 会再用）
                request.EnableBuffering();

                // 读取原始请求体（保持 UTF8 编码）
                string body;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    body = await reader.ReadToEndAsync();
                    // 重置流位置，供后续读取
                    request.Body.Position = 0;
                }

                // 从 Authorization 头部获取签名
                if (!request.Headers.TryGetValue("Authorization", out var authHeader))
                {
                    _logger.LogWarning("[Shopee] Missing Authorization header for signature verification");
                    return false;
                }

                var signatureFromHeader = authHeader.ToString();
                // 去除可能的 "Bearer " 前缀
                if (signatureFromHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    signatureFromHeader = signatureFromHeader.Substring(7).Trim();
                }

                // 从配置读取 AppSecret
                var appSecret = _config["Shopee:AppSecret"];
                if (string.IsNullOrEmpty(appSecret))
                {
                    _logger.LogError("[Shopee] AppSecret not configured in appsettings");
                    return false;
                }

                // 计算 HMAC-SHA256 签名
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var hash = hmac.ComputeHash(bodyBytes);
                var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                // 对比签名
                var isValid = computedSignature == signatureFromHeader;
                if (isValid)
                {
                    _logger.LogInformation("[Shopee] Signature verification succeeded");
                }
                else
                {
                    _logger.LogWarning("[Shopee] Signature verification failed. Computed={Computed}, Received={Received}", computedSignature, signatureFromHeader);
                }
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] Exception during signature verification");
                return false;
            }
        }
    }
}
