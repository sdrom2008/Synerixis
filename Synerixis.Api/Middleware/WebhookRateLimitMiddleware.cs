using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Synerixis.Api.Middleware
{
    public sealed class WebhookRateLimitOptions
    {
        public const string SectionName = "Webhook";
        /// <summary>每分钟允许次数（按 platform+IP）；默认 120</summary>
        public int RateLimitPerMinute { get; set; } = 120;
        /// <summary>Memory（默认，单实例）或 Redis（多实例需 IDistributedCache）。</summary>
        public string RateLimitStore { get; set; } = "Memory";
    }

    /// <summary>Webhook 固定窗口计数。Memory 单机；Redis 走 IDistributedCache。</summary>
    public interface IWebhookRateLimitStore
    {
        int Hit(string key, DateTime utcNow);
    }

    /// <summary>进程内 ConcurrentDictionary；默认。多实例互不共享。</summary>
    public sealed class MemoryWebhookRateLimitStore : IWebhookRateLimitStore
    {
        private readonly ConcurrentDictionary<string, WindowCounter> _windows = new();

        public int Hit(string key, DateTime utcNow)
        {
            var window = _windows.AddOrUpdate(
                key,
                _ => new WindowCounter(utcNow, 1),
                (_, existing) =>
                {
                    if ((utcNow - existing.WindowStart).TotalMinutes >= 1)
                        return new WindowCounter(utcNow, 1);
                    return existing with { Count = existing.Count + 1 };
                });
            return window.Count;
        }

        private sealed record WindowCounter(DateTime WindowStart, int Count);
    }

    /// <summary>
    /// 基于 IDistributedCache 的固定窗口。Webhook:RateLimitStore=Redis 且已注册 Redis cache 时使用。
    /// 非原子 INCR，限流场景可接受。
    /// </summary>
    public sealed class DistributedWebhookRateLimitStore : IWebhookRateLimitStore
    {
        private readonly IDistributedCache _cache;

        public DistributedWebhookRateLimitStore(IDistributedCache cache)
        {
            _cache = cache;
        }

        public int Hit(string key, DateTime utcNow)
        {
            var cacheKey = "whrl:" + key;
            var raw = _cache.GetString(cacheKey);
            var count = 1;
            var windowStart = utcNow;
            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split('|');
                if (parts.Length == 2
                    && DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var start)
                    && int.TryParse(parts[1], out var c)
                    && (utcNow - start).TotalMinutes < 1)
                {
                    windowStart = start;
                    count = c + 1;
                }
            }

            var remaining = TimeSpan.FromMinutes(1) - (utcNow - windowStart);
            if (remaining < TimeSpan.FromSeconds(1))
                remaining = TimeSpan.FromSeconds(1);

            _cache.SetString(
                cacheKey,
                $"{windowStart:o}|{count}",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = remaining });
            return count;
        }
    }

    /// <summary>
    /// 按 platform + IP 固定窗口限流。超限 429，不触碰幂等表。
    /// </summary>
    public sealed class WebhookRateLimitMiddleware
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly WebhookRateLimitOptions _options;
        private readonly IWebhookRateLimitStore _store;
        private readonly ILogger<WebhookRateLimitMiddleware> _logger;

        public WebhookRateLimitMiddleware(
            RequestDelegate next,
            IOptions<WebhookRateLimitOptions> options,
            IWebhookRateLimitStore store,
            ILogger<WebhookRateLimitMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _store = store;
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

            var limit = _options.RateLimitPerMinute > 0 ? _options.RateLimitPerMinute : 120;
            var platform = ExtractPlatform(path) ?? "unknown";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"{platform}:{ip}";

            var count = _store.Hit(key, DateTime.UtcNow);
            if (count > limit)
            {
                _logger.LogWarning(
                    "[WebhookRateLimit] 429 platform={Platform} ip={Ip} count={Count} limit={Limit} store={Store}",
                    platform, ip, count, limit, _options.RateLimitStore);
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
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
                && parts[1].Equals("webhook", StringComparison.OrdinalIgnoreCase))
                return parts[2].ToLowerInvariant();
            return null;
        }
    }
}
