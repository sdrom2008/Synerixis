using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Clients
{
    /// <summary>
    /// TikTok Shop 平台客户端
    /// </summary>
    public class TikTokShopPlatformClient : IPlatformClient
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TikTokShopPlatformClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public TikTokShopPlatformClient(
            IConfiguration config,
            ILogger<TikTokShopPlatformClient> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default)
        {
            var accessToken = _config["TikTok:AccessToken"];
            var basePath = _config["TikTok:BasePath"] ?? "https://open-api.tiktokshop.com";

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("[TikTok] AccessToken not configured. Skipping send reply.");
                return;
            }

            var url = $"{basePath}/v1/chat/reply?access_token={accessToken}";

            var bodyObj = new
            {
                conversation_id = context.ConversationId,
                content = new { content, msg_type = 1 },
            };
            var json = JsonSerializer.Serialize(bodyObj);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Tt-Logger", "Synerixis");

            try
            {
                var response = await http.PostAsync(url, httpContent, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("[TikTok] SendReply success: {Response}", respBody);
                }
                else
                {
                    var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("[TikTok] SendReply failed: {StatusCode}, {Body}", response.StatusCode, respBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] Exception during SendReply");
            }
        }

        public async Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!root.TryGetProperty("header", out var headerElement))
                {
                    return new PlatformMessage { Platform = "TIKTOK", Content = body, CreatedAt = DateTime.UtcNow };
                }

                if (!headerElement.TryGetProperty("event_id", out var eventId))
                    return new PlatformMessage { Platform = "TIKTOK", Content = body, CreatedAt = DateTime.UtcNow };

                string eventType = headerElement.TryGetProperty("event_type", out var eventTypeElem)
                    ? eventTypeElem.GetString() ?? "unknown"
                    : "unknown";

                var platformMsg = new PlatformMessage
                {
                    Platform = "TIKTOK",
                    MsgId = eventId.GetString(),
                    CreatedAt = DateTime.UtcNow,
                };

                if (eventType == "IM_FSM_MESSAGE")
                {
                    if (headerElement.TryGetProperty("resources", out var resourcesElement) &&
                        resourcesElement.TryGetProperty("0", out var firstResource) &&
                        firstResource.TryGetProperty("message", out var msgElem))
                    {
                        if (msgElem.TryGetProperty("text", out var textElem))
                            platformMsg.Content = textElem.GetString() ?? string.Empty;

                        if (msgElem.TryGetProperty("sender", out var senderElem))
                        {
                            if (senderElem.TryGetProperty("open_id", out var openIdElem))
                                platformMsg.CustomerId = openIdElem.GetString();
                        }

                        if (headerElement.TryGetProperty("shop_id", out var shopIdElem))
                            platformMsg.OpenId = shopIdElem.GetString();
                    }
                }

                return platformMsg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] ParseWebhook failed");
                return new PlatformMessage { Platform = "TIKTOK", Content = body, CreatedAt = DateTime.UtcNow };
            }
        }

        public async Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                request.EnableBuffering();
                string body;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    body = await reader.ReadToEndAsync();
                    request.Body.Position = 0;
                }

                // TikTok Shop uses a timestamp + body HMAC-SHA256 verification
                var appSecret = _config["TikTok:AppSecret"];
                if (string.IsNullOrEmpty(appSecret))
                {
                    _logger.LogWarning("[TikTok] AppSecret not configured. Skipping signature verification.");
                    return true; // Dev mode: skip verification
                }

                var timestamp = request.Headers.TryGetValue("x-ss-timestamp", out var ts) ? ts.ToString() : "0";
                var computed = $"{timestamp}.{body}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(computed));

                var expected = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                var actual = request.Headers.TryGetValue("x-ss-signature", out var sig) ? sig.ToString().ToLowerInvariant() : "";
                bool isValid = expected == actual;

                if (!isValid)
                    _logger.LogWarning("[TikTok] Signature verification failed");

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] Exception during signature verification");
                return false;
            }
        }

        public Task<string?> GetCustomerOrderAsync(string platform, string customerId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[TikTok] GetCustomerOrderAsync for customer: {CustomerId}", customerId);

            // TODO: Implement TikTok Shop order API
            return Task.FromResult<string?>(null);
        }
    }
}
