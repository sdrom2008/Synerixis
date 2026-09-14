using System;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    /// <summary>关键写操作审计落库（失败仅打日志，不阻断主流程）。</summary>
    public interface IAuditLogger
    {
        Task LogAsync(
            Guid? actorId,
            string actorType,
            string action,
            string? resourceType = null,
            string? resourceId = null,
            object? detail = null,
            Guid? shopId = null,
            CancellationToken ct = default);
    }
}
