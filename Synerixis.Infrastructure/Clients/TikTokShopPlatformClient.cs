using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using Synerixis.Application.DTOs;
using System.Diagnostics;
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
    /// 
    /// 修复记录 (2026-04-25):
    ///   - 签名验证: 支持 x-tts-signature (新版) 和 x-ss-signature (旧版) 双模式
    ///   - Webhook 解析: 支持 IM_MESSAGE_RECEIVED + 订单事件路由
    ///   - 回复 API: 使用 /v1/im/message/send (新版 Chat API)
    ///   - Token 刷新: 新增 RefreshAccessTokenAsync
    ///   - 事件类型: 使用 ParseWebhookResultAsync 返回 WebhookEventType 枚举
    /// </summary>
    public class TikTokShopPlatformClient : IPlatformClient
    {
        private const string ApiBasePath = "https://open-api.tiktokshop.com";
        private const int ReplyRetryCount = 3;
        private const int ReplyRetryDelayMs = 1000;

        private readonly IConfiguration _config;
        private readonly ILogger<TikTokShopPlatformClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IPlatformConnectionRepository? _connectionRepo;

        // 内存缓存 AccessToken，避免重复刷新
        private string? _cachedAccessToken;
        private DateTime _tokenCachedAt = DateTime.MinValue;

        public TikTokShopPlatformClient(
            IConfiguration config,
            ILogger<TikTokShopPlatformClient> logger,
            IHttpClientFactory httpClientFactory,
            IPlatformConnectionRepository? connectionRepo = null)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _connectionRepo = connectionRepo;
        }

        /// <summary>
        /// 获取有效的 Access Token（自动刷新过期）
        /// </summary>
        private async Task<string> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var cachedToken = _cachedAccessToken;
            if (!string.IsNullOrEmpty(cachedToken) && (DateTime.UtcNow - _tokenCachedAt).TotalHours < 23)
            {
                return cachedToken;
            }

            // Token 即将过期，尝试刷新
            var refreshToken = _config["TikTok:RefreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var (newAccessToken, newRefreshToken) = await RefreshAccessTokenAsync(refreshToken, cancellationToken);
                _cachedAccessToken = newAccessToken;
                _tokenCachedAt = DateTime.UtcNow;
                return newAccessToken;
            }

            // 从配置直接读取
            var accessToken = _config["TikTok:AccessToken"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                _cachedAccessToken = accessToken;
                _tokenCachedAt = DateTime.UtcNow;
                return accessToken;
            }

            throw new InvalidOperationException("TikTok Access Token 未配置");
        }

        /// <summary>
        /// 通过 Refresh Token 刷新 Access Token
        /// TikTok Shop Access Token 有效期约 24 小时
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken)> RefreshAccessTokenAsync(
            string refreshToken, CancellationToken cancellationToken = default)
        {
            var clientKey = _config["TikTok:ClientKey"];
            var clientSecret = _config["TikTok:ClientSecret"];

            if (string.IsNullOrEmpty(clientKey) || string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException("TikTok ClientKey/ClientSecret 未配置，无法刷新 Token");

            var basePath = _config["TikTok:BasePath"] ?? ApiBasePath;
            var url = $"{basePath}/v1/oauth/refresh_token";

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Tt-Logger", "Synerixis");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_key", clientKey),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
                new KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            var response = await http.PostAsync(url, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[TikTok] Refresh token failed: {Status} {Body}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Token 刷新失败: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var newAccessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
            var newRefreshToken = root.TryGetProperty("refresh_token", out var rtElem)
                ? rtElem.GetString() ?? refreshToken
                : refreshToken;

            _logger.LogInformation("[TikTok] Token refreshed successfully");
            return (newAccessToken, newRefreshToken);
        }

        /// <summary>
        /// 发送消息回复给 TikTok Shop 买家
        /// 使用新版 Chat API: POST /v1/im/message/send
        /// </summary>
        /// <summary>
        /// 发送回复。有则返回平台 message_id；无则 null（调用方 outbound:{guid} 兜底）。
        /// </summary>
        public async Task<string?> SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(context.ConversationId))
            {
                _logger.LogWarning("[TikTok] SendReply skipped: ConversationId is empty for msg {MsgId}", context.MsgId);
                return null;
            }

            var accessToken = await GetValidAccessTokenAsync(cancellationToken);
            var basePath = _config["TikTok:BasePath"] ?? ApiBasePath;
            var url = $"{basePath}/v1/im/message/send?access_token={accessToken}";

            // 新版 Chat API 请求体结构
            var bodyObj = new
            {
                conversation_id = context.ConversationId,
                msg_type = 1,  // 1=text, 2=image, 3=file, 4=audio, 5=video
                content = new { content },
            };

            var json = JsonSerializer.Serialize(bodyObj);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            // 带重试的发送逻辑
            var stopwatch = Stopwatch.StartNew();
            for (var retry = 0; retry < ReplyRetryCount; retry++)
            {
                using var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Add("X-Tt-Logger", "Synerixis");

                try
                {
                    var response = await http.PostAsync(url, httpContent, cancellationToken);
                    var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var platformMsgId = TryExtractPlatformMessageId(respBody);
                        _logger.LogInformation(
                            "[TikTok] SendReply success: ConversationId={ConvId}, Elapsed={Elapsed}ms, PlatformMsgId={PlatformMsgId}",
                            context.ConversationId, stopwatch.ElapsedMilliseconds, platformMsgId ?? "(none)");
                        return platformMsgId;
                    }

                    _logger.LogWarning(
                        "[TikTok] SendReply attempt {Retry}/{Max}: Status={Status}, Body={Body}",
                        retry + 1, ReplyRetryCount, response.StatusCode, respBody);

                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        // 速率限制，等待后重试
                        await Task.Delay(ReplyRetryDelayMs * (retry + 1), cancellationToken);
                        continue;
                    }

                    // 非限流错误，不重试（可能是参数错误）
                    break;
                }
                catch (Exception ex) when (retry < ReplyRetryCount - 1)
                {
                    _logger.LogWarning(ex, "[TikTok] SendReply attempt {Retry}/{Max} failed, retrying...",
                        retry + 1, ReplyRetryCount);
                    await Task.Delay(ReplyRetryDelayMs * (retry + 1), cancellationToken);
                    // 重新读取 body（重试需要重新序列化）
                    httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TikTok] SendReply failed after {Retry} retries", ReplyRetryCount);
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// 从 TikTok IM send JSON 提取 message_id（data.message_id / message_id）。
        /// 字段不稳定时返回 null，由调用方写 outbound:{localId}。
        /// </summary>
        private static string? TryExtractPlatformMessageId(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    if (data.TryGetProperty("message_id", out var mid) && mid.ValueKind == JsonValueKind.String)
                        return mid.GetString();
                    if (data.TryGetProperty("msg_id", out var msgId) && msgId.ValueKind == JsonValueKind.String)
                        return msgId.GetString();
                }
                if (root.TryGetProperty("message_id", out var top) && top.ValueKind == JsonValueKind.String)
                    return top.GetString();
            }
            catch { /* ignore */ }
            return null;
        }

        /// <summary>
        /// 解析 Webhook 请求（接口要求：返回 PlatformMessage）
        /// 调用者如需事件类型，请使用 ParseWebhookResultAsync
        /// </summary>
        public async Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            var result = await ParseWebhookResultAsync(request, cancellationToken);
            return result.Message;
        }

        /// <summary>
        /// 解析 Webhook 请求（扩展版，返回事件类型）
        /// 支持的事件类型:
        ///   - IM_MESSAGE_RECEIVED: 买家发送的消息 (聊天)
        ///   - ORDER_PAYMENT: 订单支付
        ///   - ORDER_CANCELLED: 订单取消
        ///   - ORDER_SHIPPED: 订单发货
        ///   - ORDER_COMPLETED: 订单完成
        /// </summary>
        public async Task<ParseWebhookResult> ParseWebhookResultAsync(HttpRequest request, CancellationToken cancellationToken = default)
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

                // TikTok Shop 新版 Webhook 结构:
                // {
                //   "header": { "event_id": "...", "event_type": "...", "version": "1.0", "shop_id": "..." },
                //   "body": { "message": { "open_id": "...", "conversation_id": "...", "text": "...", ... }, "order": { ... } }
                // }

                if (!root.TryGetProperty("header", out var headerElement))
                {
                    _logger.LogWarning("[TikTok] Webhook missing 'header' field, treating as raw message");
                    return new ParseWebhookResult
                    {
                        Message = new PlatformMessage { Platform = "TIKTOK", Content = body, CreatedAt = DateTime.UtcNow },
                        EventType = WebhookEventType.Unknown
                    };
                }

                var eventId = headerElement.TryGetProperty("event_id", out var eid) ? eid.GetString() : null;
                var shopId = headerElement.TryGetProperty("shop_id", out var sid) ? sid.GetString() : null;

                // 新版: event_type 带前缀 (如 "IM_MESSAGE_RECEIVED")
                var eventTypeRaw = headerElement.TryGetProperty("event_type", out var etElem)
                    ? etElem.GetString() ?? "unknown"
                    : "unknown";

                var eventType = ParseEventType(eventTypeRaw);
                var platformMsg = new PlatformMessage
                {
                    Platform = "TIKTOK",
                    MsgId = eventId,
                    OpenId = shopId ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                };

                if (root.TryGetProperty("body", out var bodyElement))
                {
                    switch (eventType)
                    {
                        case WebhookEventType.IM_MESSAGE_RECEIVED:
                            ParseImMessage(bodyElement, platformMsg);
                            break;

                        case WebhookEventType.ORDER_PAYMENT:
                        case WebhookEventType.ORDER_CANCELLED:
                        case WebhookEventType.ORDER_SHIPPED:
                        case WebhookEventType.ORDER_COMPLETED:
                        case WebhookEventType.ORDER_CREATED:
                            ParseOrderEvent(bodyElement, platformMsg, eventType);
                            break;

                        default:
                            _logger.LogDebug("[TikTok] Unhandled event type: {EventType}, raw payload", eventTypeRaw);
                            platformMsg.Content = body;
                            break;
                    }
                }
                else
                {
                    platformMsg.Content = body;
                }

                return new ParseWebhookResult { Message = platformMsg, EventType = eventType };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] ParseWebhook failed");
                return new ParseWebhookResult
                {
                    Message = new PlatformMessage { Platform = "TIKTOK", Content = body, CreatedAt = DateTime.UtcNow },
                    EventType = WebhookEventType.Unknown
                };
            }
        }

        /// <summary>
        /// 解析聊天事件
        /// payload: { "message": { "open_id": "...", "conversation_id": "...", "text": "...", "sender": { ... } } }
        /// </summary>
        private void ParseImMessage(JsonElement bodyElement, PlatformMessage msg)
        {
            if (!bodyElement.TryGetProperty("message", out var msgElem))
            {
                _logger.LogWarning("[TikTok] IM event body missing 'message' field");
                msg.Content = "未知格式的IM消息";
                return;
            }

            // open_id — 买家 ID (用于创建会话)
            if (msgElem.TryGetProperty("open_id", out var openIdElem))
                msg.CustomerId = openIdElem.GetString() ?? string.Empty;

            // conversation_id — 会话 ID (用于回复)
            if (msgElem.TryGetProperty("conversation_id", out var convIdElem))
                msg.ConversationId = convIdElem.GetString() ?? string.Empty;

            // 文本内容
            if (msgElem.TryGetProperty("text", out var textElem))
                msg.Content = textElem.GetString() ?? string.Empty;

            // 消息类型
            if (msgElem.TryGetProperty("message_type", out var mtElem))
                msg.MessageType = mtElem.GetString() ?? "text";

            // 创建时间（TikTok Shop 使用毫秒时间戳）
            if (msgElem.TryGetProperty("create_time", out var ctElem))
            {
                var createTimeMs = ctElem.GetInt64();
                if (createTimeMs > 0)
                    msg.CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(createTimeMs).UtcDateTime;
            }

            _logger.LogDebug("[TikTok] IM parsed: Customer={Customer}, Conversation={Conv}, Type={Type}",
                msg.CustomerId, msg.ConversationId, msg.MessageType);
        }

        /// <summary>
        /// 解析订单事件
        /// payload: { "order": { "order_id": "...", "order_status": "...", ... } }
        /// </summary>
        private void ParseOrderEvent(JsonElement bodyElement, PlatformMessage msg, WebhookEventType eventType)
        {
            if (!bodyElement.TryGetProperty("order", out var orderElem))
            {
                _logger.LogWarning("[TikTok] Order event body missing 'order' field");
                msg.Content = $"未知格式的{eventType}事件";
                return;
            }

            var orderId = orderElem.TryGetProperty("order_id", out var oidElem)
                ? oidElem.GetString() ?? string.Empty
                : "unknown";

            msg.CustomerId = orderId;
            msg.MessageType = eventType.ToString();

            // 订单状态
            if (orderElem.TryGetProperty("order_status", out var osElem))
                msg.Content = $"订单{eventType}: {osElem.GetString() ?? "unknown"} (OrderID: {orderId})";
            else
                msg.Content = $"订单{eventType}: {orderId}";

            _logger.LogInformation("[TikTok] Order event: {EventType} OrderId={OrderId}", eventType, orderId);
        }

        /// <summary>
        /// 将事件类型字符串映射为枚举
        /// </summary>
        private static WebhookEventType ParseEventType(string eventTypeRaw)
        {
            return eventTypeRaw.ToUpperInvariant() switch
            {
                "IM_MESSAGE_RECEIVED" => WebhookEventType.IM_MESSAGE_RECEIVED,
                "ORDER_PAYMENT"       => WebhookEventType.ORDER_PAYMENT,
                "ORDER_CANCELLED"     => WebhookEventType.ORDER_CANCELLED,
                "ORDER_SHIPPED"       => WebhookEventType.ORDER_SHIPPED,
                "ORDER_COMPLETED"     => WebhookEventType.ORDER_COMPLETED,
                "ORDER_CREATED"       => WebhookEventType.ORDER_CREATED,
                _                     => WebhookEventType.Unknown
            };
        }

        /// <summary>
        /// 验证 Webhook 请求签名
        /// 
        /// 支持两种签名格式（TikTok Shop 新旧版本）:
        ///   1. 新版: x-tts-signature + x-tts-timestamp (HMAC-SHA256, Base64 签名)
        ///   2. 旧版: x-ss-signature + x-ss-timestamp (HMAC-SHA256, Hex 签名)
        /// 
        /// 推荐配置: 在 TikTok Seller Center 中配置 Webhook 时使用新版签名。
        /// </summary>
        public async Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            var appSecret = _config["TikTok:AppSecret"];
            if (string.IsNullOrEmpty(appSecret))
            {
                _logger.LogWarning("[TikTok] AppSecret not configured. Skipping signature verification (dev mode).");
                return true;
            }

            // ── 新版签名 (x-tts-signature) ──
            if (request.Headers.TryGetValue("x-tts-signature", out var ttsSig) &&
                request.Headers.TryGetValue("x-tts-timestamp", out var ttsTs))
            {
                return VerifyTtsSignature(body, appSecret, ttsSig.ToString(), ttsTs.ToString());
            }

            // ── 旧版签名 (x-ss-signature) ──
            if (request.Headers.TryGetValue("x-ss-signature", out var ssSig) &&
                request.Headers.TryGetValue("x-ss-timestamp", out var ssTs))
            {
                return VerifySsSignature(body, appSecret, ssSig.ToString(), ssTs.ToString());
            }

            // 无签名头 — 开发环境放行
            _logger.LogWarning("[TikTok] No signature header found (x-tts-signature or x-ss-signature). Dev mode?");
            return true;
        }

        /// <summary>
        /// 新版签名验证: HMAC-SHA256(appSecret, timestamp + "\n" + body)
        /// 签名格式: Base64(HMAC-SHA256)
        /// </summary>
        private bool VerifyTtsSignature(string body, string appSecret, string receivedSig, string timestamp)
        {
            try
            {
                var baseString = $"{timestamp}\n{body}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                var computed = Convert.ToBase64String(hash);

                var isValid = computed.Equals(receivedSig, StringComparison.OrdinalIgnoreCase);
                _logger.LogDebug("[TikTok] TTS signature verify: Valid={Valid}, TS={TS}", isValid, timestamp);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] TTS signature verification error");
                return false;
            }
        }

        /// <summary>
        /// 旧版签名验证: HMAC-SHA256(appSecret, timestamp + "\n" + body)
        /// 签名格式: Hex lowercase (no dashes)
        /// </summary>
        private bool VerifySsSignature(string body, string appSecret, string receivedSig, string timestamp)
        {
            try
            {
                var baseString = $"{timestamp}\n{body}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                var isValid = computed == receivedSig.ToLowerInvariant();
                _logger.LogDebug("[TikTok] SS signature verify: Valid={Valid}, TS={TS}", isValid, timestamp);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] SS signature verification error");
                return false;
            }
        }

        public async Task<string?> GetCustomerOrderAsync(string platform, string customerId, string? platformShopId = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[TikTok] GetCustomerOrderAsync for customer: {CustomerId}", customerId);

            try
            {
                var accessToken = await GetValidAccessTokenAsync(cancellationToken);
                var basePath = _config["TikTok:BasePath"] ?? ApiBasePath;

                // TikTok Shop Order API: GET /v1/orders/list?order_ids={id}
                var url = $"{basePath}/v1/orders/list?order_ids={Uri.EscapeDataString(customerId)}&access_token={accessToken}";

                using var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.Add("X-Tt-Logger", "Synerixis");

                var response = await http.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("[TikTok] Get order failed: {Status} {Body}", response.StatusCode, errorBody);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 返回订单 JSON 摘要
                if (root.TryGetProperty("orders", out var ordersElem) && ordersElem.GetArrayLength() > 0)
                {
                    var firstOrder = ordersElem[0];
                    var orderId = firstOrder.TryGetProperty("order_id", out var oid) ? oid.GetString() : "unknown";
                    var status = firstOrder.TryGetProperty("order_status", out var os) ? os.GetString() : "unknown";
                    var total = firstOrder.TryGetProperty("total_price", out var tp) ? tp.GetString() : "0";
                    return $"OrderID: {orderId}, Status: {status}, Total: {total}";
                }

                return "未找到订单信息";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] GetCustomerOrderAsync exception");
                return null;
            }
        }

        /// <summary>
        /// TikTok Shop Open API Get Tracking：<c>GET /fulfillment/202309/orders/{order_id}/tracking</c>。
        /// 请求风格对齐本客户端（HttpClientFactory + access_token + 可选 202309 签名）；
        /// 无权限/失败返回空 checkpoints + warning，不编造轨迹。
        /// </summary>
        public async Task<PlatformTrackingInfoDto> GetTrackingInfoAsync(
            string orderSn,
            string? trackingNumber = null,
            string? platformShopId = null,
            string? orderStatusHint = null,
            CancellationToken cancellationToken = default)
        {
            var degraded = new PlatformTrackingInfoDto
            {
                TrackingNumber = trackingNumber,
                OrderStatus = orderStatusHint,
                LogisticsStatus = null,
                Checkpoints = new List<TrackingCheckpointDto>(),
                Warning = "tracking_unavailable",
                Message = string.IsNullOrWhiteSpace(trackingNumber)
                    ? "暂无运单号，轨迹暂不可用"
                    : "仅有运单号/订单状态，轨迹暂不可用"
            };

            if (string.IsNullOrWhiteSpace(orderSn))
            {
                degraded.Warning = "missing_order_sn";
                return degraded;
            }

            try
            {
                var accessToken = await TryResolveAccessTokenAsync(platformShopId, cancellationToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning("[TikTok] GetTrackingInfo missing credentials order={OrderSn}", orderSn);
                    degraded.Warning = "missing_credentials";
                    return degraded;
                }

                var basePath = (_config["TikTok:BasePath"]
                    ?? _config["TikTok:FulfillmentBasePath"]
                    ?? ApiBasePath).TrimEnd('/');
                // 签名 path 使用原文；URL 对 order_id 做转义
                var orderId = orderSn.Trim();
                var path = $"/fulfillment/202309/orders/{orderId}/tracking";
                var url = BuildTikTokSignedUrl(basePath, path, accessToken, pathForUrl: $"/fulfillment/202309/orders/{Uri.EscapeDataString(orderId)}/tracking");

                using var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation("X-Tt-Logger", "Synerixis");
                http.DefaultRequestHeaders.TryAddWithoutValidation("x-tts-access-token", accessToken);

                var response = await http.GetAsync(url, cancellationToken);
                var respBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[TikTok] get tracking HTTP {Status}: {Body}", response.StatusCode, respBody);
                    degraded.Warning = response.StatusCode is System.Net.HttpStatusCode.Forbidden
                        or System.Net.HttpStatusCode.Unauthorized
                        ? "no_permission"
                        : "api_http_failed";
                    return degraded;
                }

                return ParseTikTokTrackingResponse(respBody, trackingNumber, orderStatusHint, degraded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TikTok] GetTrackingInfoAsync exception order={OrderSn}", orderSn);
                degraded.Warning = "exception";
                return degraded;
            }
        }

        private async Task<string?> TryResolveAccessTokenAsync(string? platformShopId, CancellationToken cancellationToken)
        {
            if (_connectionRepo != null && !string.IsNullOrWhiteSpace(platformShopId))
            {
                try
                {
                    var conn = await _connectionRepo.GetByShopIdAndPlatformAsync(platformShopId, "TIKTOK");
                    if (conn != null && conn.IsActive && !string.IsNullOrWhiteSpace(conn.AccessToken))
                        return conn.AccessToken;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TikTok] PlatformConnection lookup failed for shop={ShopId}", platformShopId);
                }
            }

            try
            {
                return await GetValidAccessTokenAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TikTok] GetValidAccessTokenAsync failed");
                return null;
            }
        }

        /// <summary>
        /// 现有客户端风格：access_token 查询参数；若配置了 ClientKey/Secret 则附加 TTS 202309 HMAC 签名。
        /// </summary>
        private string BuildTikTokSignedUrl(string basePath, string path, string accessToken, string? pathForUrl = null)
        {
            var appKey = FirstNonEmpty(_config["TikTok:ClientKey"], _config["TikTok:AppKey"]);
            var appSecret = FirstNonEmpty(_config["TikTok:ClientSecret"], _config["TikTok:AppSecret"]);
            var shopCipher = _config["TikTok:ShopCipher"];
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            // 官方签名：除 sign / access_token 外的 query 按 key 排序拼接，再 secret + path + params + body + secret
            var query = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(appKey))
                query["app_key"] = appKey;
            query["timestamp"] = timestamp;
            if (!string.IsNullOrWhiteSpace(shopCipher))
                query["shop_cipher"] = shopCipher;

            var urlPath = pathForUrl ?? path;
            var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            var url = $"{basePath}{urlPath}?{qs}&access_token={Uri.EscapeDataString(accessToken)}";

            if (!string.IsNullOrEmpty(appSecret))
            {
                var paramString = string.Concat(query.Select(kv => kv.Key + kv.Value));
                var baseString = $"{appSecret}{path}{paramString}{appSecret}";
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var sign = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString)))
                    .Replace("-", "").ToLowerInvariant();
                url += $"&sign={sign}";
            }

            return url;
        }

        private PlatformTrackingInfoDto ParseTikTokTrackingResponse(
            string json,
            string? trackingNumber,
            string? orderStatusHint,
            PlatformTrackingInfoDto degraded)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int? code = null;
                if (root.TryGetProperty("code", out var codeEl))
                {
                    if (codeEl.ValueKind == JsonValueKind.Number && codeEl.TryGetInt32(out var n))
                        code = n;
                    else if (codeEl.ValueKind == JsonValueKind.String && int.TryParse(codeEl.GetString(), out var ns))
                        code = ns;
                }

                var apiMessage = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                if (code.HasValue && code.Value != 0)
                {
                    var blob = $"{code} {apiMessage}";
                    _logger.LogWarning("[TikTok] get tracking error code={Code} message={Message}", code, apiMessage);
                    degraded.Warning = LooksLikePermissionError(blob) ? "no_permission" : "api_failed";
                    return degraded;
                }

                JsonElement data = root;
                if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                    data = dataEl;

                var result = new PlatformTrackingInfoDto
                {
                    TrackingNumber = trackingNumber,
                    OrderStatus = orderStatusHint,
                    Warning = null,
                    Message = null,
                    Checkpoints = new List<TrackingCheckpointDto>()
                };

                if (TryGetString(data, "tracking_number", "tracking_no") is string tn && !string.IsNullOrWhiteSpace(tn))
                    result.TrackingNumber = tn;
                if (TryGetString(data, "logistics_status", "package_status", "status") is string ls)
                    result.LogisticsStatus = ls;

                foreach (var item in EnumerateTrackingItems(data))
                {
                    var desc = TryGetString(item, "description", "desc", "title", "message");
                    var st = TryGetString(item, "status", "logistics_status", "tracking_status");
                    var time = TryParseTikTokTime(item, "update_time_millis", "update_time", "event_time", "create_time");
                    if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(st) && time == null)
                        continue;
                    result.Checkpoints.Add(new TrackingCheckpointDto
                    {
                        Time = time,
                        Description = desc,
                        Status = st
                    });
                }

                if (!result.HasTrajectory)
                {
                    result.Warning = "no_checkpoints";
                    result.Message = "仅有运单号/订单状态，轨迹暂不可用";
                    _logger.LogInformation("[TikTok] get tracking ok but empty checkpoints");
                }
                else
                {
                    _logger.LogInformation("[TikTok] get tracking ok checkpoints={Count}", result.Checkpoints.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TikTok] parse tracking JSON failed");
                degraded.Warning = "parse_failed";
                return degraded;
            }
        }

        private static IEnumerable<JsonElement> EnumerateTrackingItems(JsonElement data)
        {
            foreach (var name in new[] { "tracking", "tracking_info", "tracking_list" })
            {
                if (data.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                        yield return item;
                    yield break;
                }
            }

            if (data.TryGetProperty("tracking_info_list", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var pkg in list.EnumerateArray())
                {
                    if (pkg.TryGetProperty("tracking_info", out var inner) && inner.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in inner.EnumerateArray())
                            yield return item;
                    }
                }
            }
        }

        private static DateTime? TryParseTikTokTime(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (!item.TryGetProperty(name, out var el))
                    continue;
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n))
                    return FromUnixFlexible(n);
                if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var ns))
                    return FromUnixFlexible(ns);
            }
            return null;
        }

        private static DateTime FromUnixFlexible(long n)
        {
            // 毫秒时间戳通常 > 10^12
            return n > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(n).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
        }

        private static string? TryGetString(JsonElement obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            return null;
        }

        private static bool LooksLikePermissionError(string blob)
        {
            return blob.Contains("permission", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("auth", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("scope", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
                || blob.Contains("105005", StringComparison.OrdinalIgnoreCase);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return null;
        }

        /// <summary>
        /// 获取 TikTok Shop OAuth 授权 URL
        /// </summary>
        public Task<string> GetAuthorizationUrlAsync(string state, string? region = null)
        {
            var clientKey = _config["TikTok:ClientKey"];
            var redirectUri = _config["TikTok:RedirectUri"];

            if (string.IsNullOrEmpty(clientKey) || string.IsNullOrEmpty(redirectUri))
                throw new InvalidOperationException("TikTok ClientKey 和 RedirectUri 未配置");

            return Task.FromResult($"https://www.tiktok.com/business/openapi/auth?client_key={clientKey}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}");
        }

        /// <summary>
        /// 通过授权码获取 Access Token 和 Refresh Token
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken)> GetAccessTokenAsync(
            string authorizationCode, string state, string? region = null, CancellationToken cancellationToken = default)
        {
            var clientKey = _config["TikTok:ClientKey"];
            var clientSecret = _config["TikTok:ClientSecret"];
            var redirectUri = _config["TikTok:RedirectUri"];

            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsync(
                "https://accounts.tiktok.com/api/oauth/token",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_key", clientKey!),
                    new KeyValuePair<string, string>("client_secret", clientSecret!),
                    new KeyValuePair<string, string>("code", authorizationCode),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri ?? string.Empty),
                    new KeyValuePair<string, string>("grant_type", "authorization_code")
                }), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[TikTok] Failed to get access token: {Error}", errorContent);
                throw new InvalidOperationException("获取 Token 失败");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
            var refreshToken = root.TryGetProperty("refresh_token", out var refreshElem)
                ? refreshElem.GetString() ?? string.Empty
                : string.Empty;

            return (accessToken, refreshToken);
        }

        /// <summary>
        /// 获取店铺信息
        /// </summary>
        public async Task<(string ShopId, string Nickname, string AvatarUrl)> GetShopInfoAsync(
            string accessToken, string? region = null, CancellationToken cancellationToken = default)
        {
            var basePath = _config["TikTok:BasePath"] ?? ApiBasePath;
            var url = $"{basePath}/v1/shop/info/get?access_token={accessToken}";

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Add("X-Tt-Logger", "Synerixis");

            var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("获取店铺信息失败");

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var shopId = root.GetProperty("shop_id").ToString();
            var nickname = root.TryGetProperty("shop_name", out var nameElem)
                ? nameElem.GetString() ?? "TikTok Shop"
                : "TikTok Shop";
            var avatar = root.TryGetProperty("logo_url", out var logoElem)
                ? logoElem.GetString() ?? string.Empty
                : string.Empty;

            return (shopId, nickname, avatar);
        }
    }

    /// <summary>
    /// TikTok Shop Webhook 事件类型枚举
    /// </summary>
    public enum WebhookEventType
    {
        Unknown = 0,

        // ── 聊天事件 ──
        IM_MESSAGE_RECEIVED = 100,

        // ── 订单事件 ──
        ORDER_CREATED = 200,
        ORDER_PAYMENT = 201,
        ORDER_SHIPPED = 202,
        ORDER_CANCELLED = 203,
        ORDER_COMPLETED = 204,
    }

    /// <summary>
    /// Webhook 解析结果（消息 + 事件类型）
    /// </summary>
    public class ParseWebhookResult
    {
        public PlatformMessage Message { get; set; } = new();
        public WebhookEventType EventType { get; set; }
    }
}
