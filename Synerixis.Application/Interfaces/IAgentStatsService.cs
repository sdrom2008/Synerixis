using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    public interface IAgentStatsService
    {
        Task<AgentPerformanceDto> GetAgentPerformanceAsync(Guid agentId, DateTime? from = null, DateTime? to = null);
        Task<IEnumerable<AgentPerformanceDto>> GetAllAgentsPerformanceAsync(Guid shopId, DateTime? from = null, DateTime? to = null);
        Task<DashboardStatsDto> GetDashboardStatsAsync(Guid shopId);
    }

    public class AgentPerformanceDto
    {
        public Guid AgentId { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int ResolvedSessions { get; set; }
        public int TotalMessages { get; set; }
        public int AiMessages { get; set; }
        public int AgentMessages { get; set; }
        public double AvgResponseTimeSeconds { get; set; }
        public double AvgResolutionTimeMinutes { get; set; }
        public double? AvgSatisfaction { get; set; }
        public int TotalOrdersQueried { get; set; } // Optional: track order queries handled
    }

    public class DashboardStatsDto
    {
        public int TotalSessions { get; set; }
        public int PendingSessions { get; set; }
        public int ActiveSessions { get; set; }
        public int ResolvedSessions { get; set; }
        public int TotalAgents { get; set; }
        public int OnlineAgents { get; set; }
        public double AvgResponseTimeSeconds { get; set; }
        public double AvgResolutionTimeMinutes { get; set; }
        public double OverallSatisfaction { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
