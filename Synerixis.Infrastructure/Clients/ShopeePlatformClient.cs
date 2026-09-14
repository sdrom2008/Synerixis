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
    /// Shopee 平台客户端（跨境电商）
    /// Phase 1: Shopee + TikTok Shop
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
        /// 发送回复给 Shopee 买家
        /// </summary>
        public async Task SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default)
        {
            // 读取配置
            var partnerId = _config["Shopee:AppKey"];
            var appSecret = _config["Shopee:AppSecret"];
            var accessToken = _config["Shopee:AccessToken"];
            var shopIdStr = _config["Shopee:ShopId"];
            var endpoint = _config["Shopee:Endpoint"] ?? "https://partner.shopeemobile.com";

            if (string.IsNullOrEmpty(partnerId) || string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(shopIdStr))
            {
                _logger.LogWarning("[Shopee] Missing configuration (AppKey/AppSecret/AccessToken/ShopId). Skipping send reply.");
                return;
            }

            if (!long.TryParse(shopIdStr, out long shopId))
            {
                _logger.LogWarning("[Shopee] Invalid ShopId value: {ShopId}", shopIdStr);
                return;
            }

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string path = "/api/v2/sellerchat/send_message";

            // 生成签名：baseString = partnerId + path + timestamp + accessToken + shopId
            string baseString = $"{partnerId}{path}{timestamp}{accessToken}{shopId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            string sign = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            // 构造请求 URL（带查询参数）
            var url = $"{endpoint}{path}?partner_id={partnerId}&timestamp={timestamp}&access_token={accessToken}&shop_id={shopId}&sign={sign}";

            // 构造请求体 JSON
            var bodyObj = new
            {
                request_id = Guid.NewGuid().ToString(),
                session_id = context.ConversationId, // 使用 webhook 中解析出的会话 ID
                txt = content
                // 可扩展：template_id, img_url 等
            };
            var json = JsonSerializer.Serialize(bodyObj);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                using var http = new HttpClient(); // 生产环境建议注入 IHttpClientFactory
                var response = await http.PostAsync(url, httpContent, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Shopee] SendReply success: Session={SessionId}, Content={Content}", context.ConversationId, content);
                }
                else
                {
                    var respBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[Shopee] SendReply failed: {StatusCode}, {Body}", response.StatusCode, respBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] Exception during SendReply");
            }
        }

        /// <summary>
        /// 解析 Webhook 请求（Shopee 聊天推送）
        /// </summary>
        public async Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            // 请求体已被 VerifySignatureAsync 开启缓冲并读取，但这里需要再次读取（确保重置）
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

                // 必需字段：to_shop_id, data
                if (!root.TryGetProperty("to_shop_id", out var toShopIdElement) ||
                    !root.TryGetProperty("data", out var dataElement))
                {
                    _logger.LogWarning("[Shopee] Webhook missing required fields (to_shop_id or data)");
                    return new PlatformMessage
                    {
                        Platform = "SHOPEE",
                        Content = body,
                        CreatedAt = DateTime.UtcNow
                    };
                }

                long toShopId = toShopIdElement.GetInt64();

                // 只处理类型为 "message" 的推送（买家发送的消息）
                if (dataElement.TryGetProperty("type", out var typeElement) && typeElement.GetString() == "message" &&
                    dataElement.TryGetProperty("content", out var contentElement))
                {
                    string messageType = "text";
                    if (contentElement.TryGetProperty("message_type", out var mtElem))
                        messageType = mtElem.GetString() ?? "text";

                    string message = string.Empty;
                    if (contentElement.TryGetProperty("message", out var msgElem))
                        message = msgElem.GetString() ?? string.Empty;

                    long fromUserId = 0;
                    if (contentElement.TryGetProperty("from_user_id", out var fuElem))
                        fromUserId = fuElem.GetInt64();

                    string fromUserName = string.Empty;
                    if (contentElement.TryGetProperty("from_user_name", out var funElem))
                        fromUserName = funElem.GetString() ?? string.Empty;

                    string? conversationId = null;
                    if (contentElement.TryGetProperty("conversation_id", out var convElem))
                        conversationId = convElem.GetString();

                    long createTime = 0;
                    if (contentElement.TryGetProperty("create_time", out var ctElem))
                        createTime = ctElem.GetInt64();

                    var platformMsg = new PlatformMessage
                    {
                        Platform = "SHOPEE",
                        OpenId = toShopId.ToString(), // 店铺 ID（商户标识）
                        CustomerId = fromUserId.ToString(),
                        CustomerName = fromUserName,
                        Content = message,
                        MessageType = messageType,
                        MsgId = root.TryGetProperty("push_id", out var pushIdElem) ? pushIdElem.GetString() : null,
                        ConversationId = conversationId,
                        CreatedAt = createTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(createTime).UtcDateTime : DateTime.UtcNow
                    };
                    return platformMsg;
                }
                else
                {
                    // 非聊天消息（如通知），原样返回
                    return new PlatformMessage
                    {
                        Platform = "SHOPEE",
                        OpenId = toShopId.ToString(),
                        Content = body,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] ParseWebhook failed");
                return new PlatformMessage
                {
                    Platform = "SHOPEE",
                    Content = body,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// 验证 Webhook 签名（Shopee 使用 HMAC-SHA256，签名位于 x-shopee-signature 或 Authorization 头）
        /// </summary>
        public async Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // 开启请求体缓冲，以便多次读取
                request.EnableBuffering();

                // 读取原始请求体（字符串形式）
                string body;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    body = await reader.ReadToEndAsync();
                    // 重置流位置，供后续读取
                    request.Body.Position = 0;
                }

                // 获取签名头：优先 x-shopee-signature，其次 Authorization（可能有 Bearer 前缀）
                if (!request.Headers.TryGetValue("x-shopee-signature", out var sigHeader) &&
                    !request.Headers.TryGetValue("Authorization", out sigHeader))
                {
                    _logger.LogWarning("[Shopee] Missing signature header (x-shopee-signature or Authorization)");
                    return false;
                }
                string signature = sigHeader.ToString();
                if (signature.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    signature = signature.Substring(7).Trim();
                }

                var appSecret = _config["Shopee:AppSecret"];
                if (string.IsNullOrEmpty(appSecret))
                {
                    _logger.LogError("[Shopee] AppSecret not configured");
                    return false;
                }

                // 构造回调 URL：scheme://host/path
                string callbackUrl = $"{request.Scheme}://{request.Host}{request.Path}";

                // 拼接签名字符串：callbackUrl + "|" + body
                string baseString = $"{callbackUrl}|{body}";

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                string computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                bool isValid = computedSignature == signature;
                if (isValid)
                {
                    _logger.LogInformation("[Shopee] Signature verification succeeded");
                }
                else
                {
                    _logger.LogWarning("[Shopee] Signature verification failed. Computed={Computed}, Received={Received}", computedSignature, signature);
                }
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] Exception during signature verification");
                return false;
            }
        }

        /// <summary>
        /// 查询买家订单摘要（Shopee Open API v2）。
        /// customerId 若像 order_sn（含字母）则走 get_order_detail；
        /// 否则按 buyer_user_id 在近期订单列表中匹配。
        /// 缺配置时仅打警告并返回 null，不抛异常。
        /// </summary>
        public async Task<string?> GetCustomerOrderAsync(string platform, string customerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                _logger.LogWarning("[Shopee] GetCustomerOrderAsync called with empty customerId");
                return null;
            }

            var partnerId = _config["Shopee:AppKey"];
            var appSecret = _config["Shopee:AppSecret"];
            var accessToken = _config["Shopee:AccessToken"];
            var shopIdStr = _config["Shopee:ShopId"];
            var endpoint = _config["Shopee:Endpoint"] ?? "https://partner.shopeemobile.com";

            if (string.IsNullOrEmpty(partnerId) || string.IsNullOrEmpty(appSecret) ||
                string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(shopIdStr))
            {
                _logger.LogWarning("[Shopee] Missing configuration (AppKey/AppSecret/AccessToken/ShopId). Skipping order lookup.");
                return null;
            }

            if (!long.TryParse(shopIdStr, out long shopId))
            {
                _logger.LogWarning("[Shopee] Invalid ShopId value: {ShopId}", shopIdStr);
                return null;
            }

            try
            {
                // order_sn 通常含字母（如 220314ABCDEFGH）；纯数字按 buyer_user_id 处理
                bool looksLikeOrderSn = customerId.Any(char.IsLetter);

                if (looksLikeOrderSn)
                {
                    var detail = await ShopeeGetOrderDetailAsync(
                        endpoint, partnerId, appSecret, accessToken, shopId,
                        new[] { customerId }, cancellationToken);
                    if (detail == null || detail.Count == 0)
                    {
                        _logger.LogInformation("[Shopee] No order detail for order_sn={OrderSn}", customerId);
                        return null;
                    }
                    return FormatOrderSummary(detail[0]);
                }

                // 近期窗口（默认 14 天）拉列表，再批量 detail 匹配 buyer_user_id
                var orderSns = await ShopeeGetRecentOrderSnListAsync(
                    endpoint, partnerId, appSecret, accessToken, shopId, cancellationToken);
                if (orderSns.Count == 0)
                {
                    _logger.LogInformation("[Shopee] Recent order list empty for shop={ShopId}", shopId);
                    return null;
                }

                // get_order_detail 单次最多 50 个 order_sn
                for (int i = 0; i < orderSns.Count; i += 50)
                {
                    var batch = orderSns.Skip(i).Take(50).ToArray();
                    var details = await ShopeeGetOrderDetailAsync(
                        endpoint, partnerId, appSecret, accessToken, shopId, batch, cancellationToken);
                    if (details == null) continue;

                    foreach (var order in details)
                    {
                        var buyerId = order.TryGetProperty("buyer_user_id", out var bu)
                            ? bu.ToString()
                            : null;
                        if (!string.IsNullOrEmpty(buyerId) &&
                            string.Equals(buyerId, customerId, StringComparison.Ordinal))
                        {
                            return FormatOrderSummary(order);
                        }
                    }
                }

                _logger.LogInformation("[Shopee] No recent order matched buyer_user_id={BuyerId}", customerId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Shopee] GetCustomerOrderAsync exception for customer={CustomerId}", customerId);
                return null;
            }
        }

        private async Task<List<string>> ShopeeGetRecentOrderSnListAsync(
            string endpoint, string partnerId, string appSecret, string accessToken, long shopId,
            CancellationToken cancellationToken)
        {
            string path = "/api/v2/order/get_order_list";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string sign = ComputeShopApiSign(partnerId, path, timestamp, accessToken, shopId, appSecret);
            var url = $"{endpoint}{path}?partner_id={partnerId}&timestamp={timestamp}&access_token={accessToken}&shop_id={shopId}&sign={sign}";

            // 近 14 天 create_time 窗口
            long timeTo = timestamp;
            long timeFrom = timestamp - 14 * 24 * 3600;
            var bodyObj = new
            {
                time_range_field = "create_time",
                time_from = timeFrom,
                time_to = timeTo,
                page_size = 50,
                cursor = ""
            };
            var json = JsonSerializer.Serialize(bodyObj);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            using var http = new HttpClient();
            var response = await http.PostAsync(url, httpContent, cancellationToken);
            var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Shopee] get_order_list failed: {Status} {Body}", response.StatusCode, respBody);
                return new List<string>();
            }

            var result = new List<string>();
            using var doc = JsonDocument.Parse(respBody);
            if (!doc.RootElement.TryGetProperty("response", out var resp))
                return result;

            if (resp.TryGetProperty("order_list", out var orderList) && orderList.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in orderList.EnumerateArray())
                {
                    if (item.TryGetProperty("order_sn", out var snElem))
                    {
                        var sn = snElem.GetString();
                        if (!string.IsNullOrEmpty(sn))
                            result.Add(sn);
                    }
                }
            }
            return result;
        }

        private async Task<List<JsonElement>?> ShopeeGetOrderDetailAsync(
            string endpoint, string partnerId, string appSecret, string accessToken, long shopId,
            IReadOnlyList<string> orderSnList, CancellationToken cancellationToken)
        {
            if (orderSnList == null || orderSnList.Count == 0)
                return new List<JsonElement>();

            string path = "/api/v2/order/get_order_detail";
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string sign = ComputeShopApiSign(partnerId, path, timestamp, accessToken, shopId, appSecret);
            string orderSnCsv = string.Join(",", orderSnList);
            // 请求物流相关可选字段，便于摘要里带 tracking
            string optional = "buyer_user_id,package_list";
            var url =
                $"{endpoint}{path}?partner_id={partnerId}&timestamp={timestamp}&access_token={accessToken}" +
                $"&shop_id={shopId}&sign={sign}&order_sn_list={Uri.EscapeDataString(orderSnCsv)}" +
                $"&response_optional_fields={Uri.EscapeDataString(optional)}";

            using var http = new HttpClient();
            var response = await http.GetAsync(url, cancellationToken);
            var respBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[Shopee] get_order_detail failed: {Status} {Body}", response.StatusCode, respBody);
                return null;
            }

            using var doc = JsonDocument.Parse(respBody);
            if (!doc.RootElement.TryGetProperty("response", out var resp) ||
                !resp.TryGetProperty("order_list", out var orderList) ||
                orderList.ValueKind != JsonValueKind.Array)
            {
                return new List<JsonElement>();
            }

            // Clone elements so they survive after disposing the document
            var list = new List<JsonElement>();
            foreach (var item in orderList.EnumerateArray())
            {
                list.Add(item.Clone());
            }
            return list;
        }

        private static string ComputeShopApiSign(
            string partnerId, string path, long timestamp, string accessToken, long shopId, string appSecret)
        {
            // 与 SendReplyAsync 一致：partnerId + path + timestamp + accessToken + shopId
            string baseString = $"{partnerId}{path}{timestamp}{accessToken}{shopId}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string FormatOrderSummary(JsonElement order)
        {
            var orderSn = order.TryGetProperty("order_sn", out var sn) ? sn.GetString() ?? "?" : "?";
            var status = order.TryGetProperty("order_status", out var st) ? st.GetString() ?? "?" : "?";

            string? tracking = null;
            if (order.TryGetProperty("package_list", out var packages) && packages.ValueKind == JsonValueKind.Array)
            {
                foreach (var pkg in packages.EnumerateArray())
                {
                    if (pkg.TryGetProperty("tracking_number", out var tn))
                    {
                        var t = tn.GetString();
                        if (!string.IsNullOrEmpty(t))
                        {
                            tracking = t;
                            break;
                        }
                    }
                }
            }

            return string.IsNullOrEmpty(tracking)
                ? $"order_sn={orderSn}, status={status}"
                : $"order_sn={orderSn}, status={status}, tracking={tracking}";
        }

        /// <summary>
        /// 获取 Shopee 授权 URL
        /// </summary>
        public async Task<string> GetAuthorizationUrlAsync(string state)
        {
            var appKey = _config["Shopee:AppKey"];
            var redirectUri = _config["Shopee:RedirectUri"];
            
            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(redirectUri))
                throw new InvalidOperationException("Shopee AppKey 和 RedirectUri 未配置");

            return $"https://partner.shopeemobile.com/mobile/openplatform/seller?appkey={appKey}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={state}";
        }

        /// <summary>
        /// 通过授权码获取 Token
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken)> GetAccessTokenAsync(string authorizationCode, string state, CancellationToken cancellationToken = default)
        {
            var appKey = _config["Shopee:AppKey"];
            var appSecret = _config["Shopee:AppSecret"];
            var redirectUri = _config["Shopee:RedirectUri"];

            var body = $"appkey={appKey}&code={authorizationCode}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
            var hash = GenerateSignature(appSecret, body);

            var endpoint = _config["Shopee:Endpoint"] ?? "https://partner.shopeemobile.com";
            var url = $"{endpoint}/api/v2/auth/app_token";

            var httpContent = new StringContent($"{body}&sign={hash}", Encoding.UTF8, "application/x-www-form-urlencoded");
            
            using var http = new HttpClient();
            var response = await http.PostAsync(url, httpContent, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[Shopee] Failed to get access token: {Error}", errorContent);
                throw new InvalidOperationException("获取 Token 失败");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var accessToken = root.GetProperty("access_token").GetString();
            var refreshToken = root.TryGetProperty("refresh_token", out var refreshElem) ? refreshElem.GetString() : string.Empty;

            return (accessToken, refreshToken);
        }

        /// <summary>
        /// 获取店铺信息
        /// </summary>
        public async Task<(string ShopId, string Nickname, string AvatarUrl)> GetShopInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            var appKey = _config["Shopee:AppKey"];
            var appSecret = _config["Shopee:AppSecret"];
            var endpoint = _config["Shopee:Endpoint"] ?? "https://partner.shopeemobile.com";

            // 注意：Shopee Partner API 需要先用 app_token 获取 shop_id，这里简化处理，假设先通过某种方式拿到 shopId
            // 实际流程通常是：App Token -> Get Shop List -> Get Shop Info
            // 这里我们返回一个占位符，因为完整的绑定流程需要先获取 Shop List
            
            var url = $"{endpoint}/api/v2/shop/get?access_token={accessToken}";
            
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("x-api-sign", GenerateSignature(appSecret, "shop_id")); // Simplified for demo
            var response = await http.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("获取店铺信息失败");

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Shopee GetShop 返回的是列表，取第一个
            var shopId = root.GetProperty("shop_id").ToString();
            var nickname = root.TryGetProperty("nickname", out var nickElem) ? nickElem.GetString() : "Shopee 店铺";
            
            return (shopId, nickname, string.Empty);
        }

        private string GenerateSignature(string secret, string body)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
