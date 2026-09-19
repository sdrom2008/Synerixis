using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 规则分流：将会话分配给坐席/队列（非 AI 路由）。
    /// </summary>
    public interface ISessionAssignmentService
    {
        /// <summary>
        /// 按店铺 AssignmentMode 尝试自动分配（仅未分配会话）。
        /// LeastLoaded → 负载最低的有效 Agent；Unassigned → no-op。
        /// </summary>
        Task<Agent?> TryAutoAssignAsync(ChatSession session, CancellationToken ct = default);

        /// <summary>
        /// 显式规则预设：least_loaded | supervisor（关键词/升级队列 → 任一有效 Supervisor）。
        /// </summary>
        Task<Agent?> AssignByPresetAsync(ChatSession session, string preset, CancellationToken ct = default);
    }
}
