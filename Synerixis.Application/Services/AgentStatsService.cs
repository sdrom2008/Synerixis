using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Synerixis.Application.Services
{
    public class AgentStatsService : IAgentStatsService
    {
        private readonly IRepository<Agent> _agentRepo;
        private readonly IRepository<ChatSession> _sessionRepo;

        public AgentStatsService(IRepository<Agent> agentRepo, IRepository<ChatSession> sessionRepo)
        {
            _agentRepo = agentRepo;
            _sessionRepo = sessionRepo;
        }

        public async Task<AgentPerformanceDto> GetAgentPerformanceAsync(Guid agentId, DateTime? from = null, DateTime? to = null)
        {
            var agent = await _agentRepo.GetByIdAsync(agentId);
            if (agent == null)
                return new AgentPerformanceDto { AgentId = agentId, AgentName = "Unknown" };

            var shopId = agent.ShopId;

            Expression<Func<ChatSession, bool>> predicate = s =>
                s.AssignedAgentId == agentId && s.ShopId == shopId;
            if (from.HasValue)
                predicate = predicate.And(s => s.CreatedAt >= from.Value);
            if (to.HasValue)
                predicate = predicate.And(s => s.CreatedAt <= to.Value);

            var sessions = await _sessionRepo.GetAllAsync(predicate: predicate);
            var resolved = sessions.Where(s => s.Status == SessionStatus.Resolved).ToList();

            var stats = new AgentPerformanceDto
            {
                AgentId = agentId,
                AgentName = agent.Name,
                TotalSessions = sessions.Count,
                ActiveSessions = sessions.Count(s => s.Status == SessionStatus.Active),
                ResolvedSessions = resolved.Count,
                TotalMessages = sessions.Sum(s => s.MessageCount),
                AiMessages = sessions.Sum(s => s.AiMessageCount),
                AgentMessages = sessions.Sum(s => s.AgentMessageCount),
                AvgResponseTimeSeconds = resolved.Any() ? resolved.Average(s => s.ResponseTime?.TotalSeconds ?? 0) : 0,
                AvgResolutionTimeMinutes = resolved.Any() ? resolved.Average(s => s.ResolutionTime?.TotalMinutes ?? 0) : 0,
                AvgSatisfaction = resolved.Any() ? resolved.Select(s => (double?)s.Satisfaction).Average() : null,
                TotalOrdersQueried = 0
            };

            return stats;
        }

        public async Task<IEnumerable<AgentPerformanceDto>> GetAllAgentsPerformanceAsync(Guid shopId, DateTime? from = null, DateTime? to = null)
        {
            var agents = await _agentRepo.GetAllAsync(a => a.ShopId == shopId);
            var results = new List<AgentPerformanceDto>();

            foreach (var agent in agents)
            {
                var stats = await GetAgentPerformanceAsync(agent.Id, from, to);
                results.Add(stats);
            }

            return results;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid shopId)
        {
            var sessions = await _sessionRepo.GetAllAsync(s => s.ShopId == shopId);
            var agents = await _agentRepo.GetAllAsync(a => a.ShopId == shopId);

            var resolved = sessions.Where(s => s.Status == SessionStatus.Resolved).ToList();

            var stats = new DashboardStatsDto
            {
                TotalSessions = sessions.Count,
                PendingSessions = sessions.Count(s => s.Status == SessionStatus.Pending),
                ActiveSessions = sessions.Count(s => s.Status == SessionStatus.Active),
                ResolvedSessions = resolved.Count,
                TotalAgents = agents.Count,
                OnlineAgents = agents.Count(a => a.IsOnline),
                AvgResponseTimeSeconds = resolved.Any() ? resolved.Average(s => s.ResponseTime?.TotalSeconds ?? 0) : 0,
                AvgResolutionTimeMinutes = resolved.Any() ? resolved.Average(s => s.ResolutionTime?.TotalMinutes ?? 0) : 0,
                OverallSatisfaction = resolved.Any() && resolved.Any(s => s.Satisfaction.HasValue)
                    ? resolved.Where(s => s.Satisfaction.HasValue).Select(s => (double)s.Satisfaction.Value).Average()
                    : 0,
                LastUpdated = DateTime.UtcNow
            };

            return stats;
        }
    }

    // Expression predicate combiner
    public static class PredicateBuilder
    {
        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var parameter = Expression.Parameter(typeof(T));
            var replacedExpr2 = new ParameterReplacer(expr2.Parameters[0], parameter).Visit(expr2.Body);
            var body = Expression.AndAlso(expr1.Body, replacedExpr2);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }
    }
}
