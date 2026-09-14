using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Services
{
    public class AuditLogger : IAuditLogger
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly AppDbContext _db;
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(AppDbContext db, ILogger<AuditLogger> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task LogAsync(
            Guid? actorId,
            string actorType,
            string action,
            string? resourceType = null,
            string? resourceId = null,
            object? detail = null,
            Guid? shopId = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(action)) return;

            try
            {
                string? detailJson = null;
                if (detail != null)
                {
                    detailJson = detail is string s
                        ? s
                        : JsonSerializer.Serialize(detail, JsonOpts);
                    if (detailJson.Length > 4000)
                        detailJson = detailJson.Substring(0, 4000);
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActorId = actorId,
                    ActorType = string.IsNullOrWhiteSpace(actorType) ? "System" : actorType.Trim(),
                    Action = action.Trim(),
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    DetailJson = detailJson,
                    CreatedAt = DateTime.UtcNow,
                    ShopId = shopId
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Audit] Failed to persist Action={Action} Actor={ActorType}/{ActorId}",
                    action, actorType, actorId);
            }
        }
    }
}
