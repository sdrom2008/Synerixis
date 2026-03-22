using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 客服绩效统计表（每日汇总）
    /// 通过定时任务（如每小时）计算并填充
    /// </summary>
    public class AgentStat : AggregateRoot<Guid>
    {
        public Guid AgentId { get; private set; }                    // 客服ID
        public DateTime StatDate { get; private set; }               // 统计日期（YYYY-MM-DD）

        // 会话统计
        public int TotalConversations { get; private set; } = 0;     // 总接待对话数
        public int PendingCount { get; private set; } = 0;           // 待处理数
        public int ActiveCount { get; private set; } = 0;            // 进行中数
        public int ResolvedCount { get; private set; } = 0;          // 已解决数
        public int ClosedCount { get; private set; } = 0;            // 已关闭数

        // 响应效率
        public int TotalResponseTimeSeconds { get; private set; } = 0; // 总响应时长（秒）
        public double AvgResponseTimeSeconds { get; private set; } = 0; // 平均响应时长
        public int? AvgFirstResponseTimeSeconds { get; private set; }   // 平均首次响应时长

        // 消息统计
        public int TotalMessages { get; private set; } = 0;           // 消息总数
        public int AgentMessages { get; private set; } = 0;           // 人工消息数
        public int AiMessages { get; private set; } = 0;              // AI消息数

        // 客户满意度
        public int? SatisfactionCount { get; private set; } = 0;      // 评分数量
        public double? AvgSatisfaction { get; private set; }          // 平均满意度（1-5分）

        // 解决率
        public double ResolutionRate { get; private set; } = 0;       // 解决率 = Resolved / Total

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }

        // 导航
        public Agent? Agent { get; private set; }

        private AgentStat() { }

        /// <summary>
        /// 创建当日统计记录
        /// </summary>
        public static AgentStat Create(Guid agentId, DateTime date)
        {
            return new AgentStat
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                StatDate = date.Date,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 每日汇总计算（根据当天对话数据）
        /// </summary>
        public void Calculate(
            int totalConversations,
            int resolvedCount,
            int totalMessages,
            int agentMessages,
            int aiMessages,
            double avgResponseTime,
            double? avgSatisfaction)
        {
            TotalConversations = totalConversations;
            ResolvedCount = resolvedCount;
            ResolutionRate = totalConversations > 0 ? (double)resolvedCount / totalConversations : 0;

            TotalMessages = totalMessages;
            AgentMessages = agentMessages;
            AiMessages = aiMessages;

            AvgResponseTimeSeconds = avgResponseTime;
            AvgSatisfaction = avgSatisfaction;

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 累加会话计数器（每日定时任务中调用）
        /// </summary>
        public void IncrementConversation(SessionStatus status, bool isResolved)
        {
            TotalConversations++;

            switch (status)
            {
                case SessionStatus.Pending:
                    PendingCount++;
                    break;
                case SessionStatus.Active:
                    ActiveCount++;
                    break;
                case SessionStatus.Resolved:
                    ResolvedCount++;
                    break;
                case SessionStatus.Closed:
                    ClosedCount++;
                    break;
            }

            if (isResolved) ResolvedCount++;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
