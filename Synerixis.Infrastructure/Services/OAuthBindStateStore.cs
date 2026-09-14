using System;
using System.Collections.Concurrent;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// 绑店 OAuth state 短存（进程内）。生产可换 Redis；开发可用 query platform 兜底。
    /// </summary>
    public interface IOAuthBindStateStore
    {
        void Put(string state, Guid sellerId, string platform, string? region = null, TimeSpan? ttl = null);
        bool TryTake(string state, out Guid sellerId, out string platform, out string? region);
    }

    public sealed class OAuthBindStateStore : IOAuthBindStateStore
    {
        private readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.Ordinal);

        private sealed record Entry(Guid SellerId, string Platform, string? Region, DateTime ExpiresAtUtc);

        public void Put(string state, Guid sellerId, string platform, string? region = null, TimeSpan? ttl = null)
        {
            if (string.IsNullOrWhiteSpace(state)) return;
            var exp = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(30));
            var reg = string.IsNullOrWhiteSpace(region) ? null : region.Trim().ToUpperInvariant();
            _map[state] = new Entry(sellerId, platform.Trim().ToUpperInvariant(), reg, exp);
            CleanupExpired();
        }

        public bool TryTake(string state, out Guid sellerId, out string platform, out string? region)
        {
            sellerId = default;
            platform = string.Empty;
            region = null;
            if (string.IsNullOrWhiteSpace(state)) return false;
            if (!_map.TryRemove(state, out var entry)) return false;
            if (entry.ExpiresAtUtc < DateTime.UtcNow) return false;
            sellerId = entry.SellerId;
            platform = entry.Platform;
            region = entry.Region;
            return true;
        }

        private void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _map)
            {
                if (kv.Value.ExpiresAtUtc < now)
                    _map.TryRemove(kv.Key, out _);
            }
        }
    }
}
