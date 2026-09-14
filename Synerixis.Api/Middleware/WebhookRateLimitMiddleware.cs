using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Synerixis.Api.Middleware
{
    public sealed class WebhookRateLimitOptions
    {
        public const string SectionName = "Webhook";
        /// <summary>每分钟允许次数（按 platform+IP）；默认 120</summary>
        public int RateLimitPerMinute { get; set; } = 120;
    }

    /// <summary>
    /// 简单内存限流：按 platform + IP 固定窗口。超限 429，不触碰幂等表。
    /// </summary>
    public sealed class WebhookRateLimitMiddleware
    {
        private static readonly ConcurrentDictionary<string, WindowCounter> Windows = new();
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly WebhookRateLimitOptions _options;
        private readonly ILogger<WebhookRateLimitMiddleware> _logger;

        public WebhookRateLimitMiddleware(
            RequestDelegate next,
            IOptions<WebhookRateLimitOptions> options,
            ILogger<WebhookRateLimitMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/api/webhook", StringComparison.OrdinalIgnoreCase)
                || !HttpMethods.IsPost(context.Request.Method))
            {
                await _next(context);
                return;
            }

            // 跳过 */test 方便本地压测说明；仍可被配置限流（默认也限）
            var limit = _options.RateLimitPerMinute > 0 ? _options.RateLimitPerMinute : 120;
            var platform = ExtractPlatform(path) ?? "unknown";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"{platform}:{ip}";

            var now = DateTime.UtcNow;
            var window = Windows.AddOrUpdate(
                key,
                _ => new WindowCounter(now, 1),
                (_, existing) =>
                {
                    if ((now - existing.WindowStart).TotalMinutes >= 1)
                        return new WindowCounter(now, 1);
                    return existing with { Count = existing.Count + 1 };
                });

            if (window.Count > limit)
            {
                _logger.LogWarning(
                    "[WebhookRateLimit] 429 platform={Platform} ip={Ip} count={Count} limit={Limit}",
                    platform, ip, window.Count, limit);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    code = "RATE_LIMIT",
                    message = $"Webhook rate limit exceeded ({limit}/min)"
                }, JsonOpts));
                return;
            }

            await _next(context);
        }

        private static string? ExtractPlatform(string path)
        {
            // /api/webhook/{platform} or /api/webhook/{platform}/test
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
                && parts[1].Equals("webhook", StringComparison.OrdinalIgnoreCase))
                return parts[2].ToLowerInvariant();
            return null;
        }

        private sealed record WindowCounter(DateTime WindowStart, int Count);
    }
}
