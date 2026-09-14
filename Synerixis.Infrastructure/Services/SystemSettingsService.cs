using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Services
{
    public sealed class SystemSettingsService : ISystemSettingsService
    {
        public const string CacheKey = "ops:system_settings";
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;

        public SystemSettingsService(AppDbContext db, IMemoryCache cache, IConfiguration config)
        {
            _db = db;
            _cache = cache;
            _config = config;
        }

        public async Task<SystemOpsSettings> GetOpsAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out SystemOpsSettings? cached) && cached != null)
                return cached;

            var rows = await _db.SystemSettings.AsNoTracking()
                .Where(s => SystemSettingKeys.WritableKeys.Contains(s.Key))
                .ToListAsync(ct);

            string? Get(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value;

            var maint = Get(SystemSettingKeys.MaintenanceMode)
                ?? _config["Ops:MaintenanceMode"]
                ?? "false";
            var mode = Get(SystemSettingKeys.DefaultOutboundMode)
                ?? _config["Ops:DefaultOutboundMode"]
                ?? _config["CustomerService:OutboundMode"]
                ?? OutboundModes.DraftFirst;
            var allowReg = Get(SystemSettingKeys.AllowNewRegistration)
                ?? _config["Ops:AllowNewRegistration"]
                ?? "true";

            var ops = new SystemOpsSettings(
                IsTruthy(maint),
                OutboundModes.Normalize(mode),
                IsTruthy(allowReg));

            _cache.Set(CacheKey, ops, CacheTtl);
            return ops;
        }

        public void Invalidate() => _cache.Remove(CacheKey);

        private static bool IsTruthy(string? v) =>
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
    }
}
