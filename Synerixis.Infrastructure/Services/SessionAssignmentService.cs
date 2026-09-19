using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>最小可用规则分流：未分配队列 / 最少负载 Agent / Supervisor 预设。</summary>
    public class SessionAssignmentService : ISessionAssignmentService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SessionAssignmentService> _logger;

        public SessionAssignmentService(AppDbContext db, ILogger<SessionAssignmentService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Agent?> TryAutoAssignAsync(ChatSession session, CancellationToken ct = default)
        {
            if (session.AssignedAgentId.HasValue)
                return null;
            if (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Resolved)
                return null;

            var mode = await _db.SellerConfigs.AsNoTracking()
                .Where(c => c.SellerId == session.ShopId)
                .Select(c => c.AssignmentMode)
                .FirstOrDefaultAsync(ct);

            if (!AssignmentModes.IsLeastLoaded(mode))
                return null;

            return await AssignByPresetAsync(session, "least_loaded", ct);
        }

        public async Task<Agent?> AssignByPresetAsync(ChatSession session, string preset, CancellationToken ct = default)
        {
            if (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Resolved)
                return null;

            var key = (preset ?? "").Trim().ToLowerInvariant();
            Agent? pick = key switch
            {
                "supervisor" or "to_supervisor" => await PickSupervisorAsync(session.ShopId, ct),
                "least_loaded" or "leastloaded" or "round_robin" => await PickLeastLoadedAgentAsync(session.ShopId, ct),
                _ => null
            };

            if (pick == null)
            {
                _logger.LogInformation(
                    "[Assign] preset={Preset} no candidate shop={ShopId} session={SessionId}",
                    key, session.ShopId, session.Id);
                return null;
            }

            session.AssignToAgent(pick.Id);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "[Assign] preset={Preset} agent={AgentId} {Name} → session={SessionId}",
                key, pick.Id, pick.Name, session.Id);
            return pick;
        }

        private async Task<Agent?> PickLeastLoadedAgentAsync(Guid shopId, CancellationToken ct)
        {
            var open = new[] { SessionStatus.Pending, SessionStatus.Active };
            var agents = await _db.Agents.AsNoTracking()
                .Where(a => a.ShopId == shopId && a.IsActive && a.Role == AgentRole.Agent)
                .Select(a => new { a.Id, a.Name, a.Role, a.IsOnline, a.IsActive })
                .ToListAsync(ct);
            if (agents.Count == 0)
                return null;

            // 在线优先；若无人在线则全体有效 Agent
            var pool = agents.Where(a => a.IsOnline).ToList();
            if (pool.Count == 0)
                pool = agents;

            var loads = await _db.ChatSessions.AsNoTracking()
                .Where(s => s.ShopId == shopId && s.AssignedAgentId != null && open.Contains(s.Status))
                .GroupBy(s => s.AssignedAgentId!.Value)
                .Select(g => new { AgentId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var loadMap = loads.ToDictionary(x => x.AgentId, x => x.Count);

            var best = pool
                .OrderBy(a => loadMap.TryGetValue(a.Id, out var c) ? c : 0)
                .ThenBy(a => a.Id)
                .First();

            return await _db.Agents.FirstAsync(a => a.Id == best.Id, ct);
        }

        private async Task<Agent?> PickSupervisorAsync(Guid shopId, CancellationToken ct)
        {
            var q = _db.Agents.Where(a => a.ShopId == shopId && a.IsActive && a.Role == AgentRole.Supervisor);
            var online = await q.FirstOrDefaultAsync(a => a.IsOnline, ct);
            if (online != null) return online;
            return await q.OrderBy(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        }
    }
}
