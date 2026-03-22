using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 客服会话状态（用于管理和分配对话）
    /// 每个客户对话对应一个ChatSession
    /// 状态流转：Pending -> Active -> Resolved -> Closed
    /// </summary>
    public class ChatSession : AggregateRoot<Guid>
    {
        public string SessionId { get; private set; } = string.Empty;  // 业务唯一标识（可用于前端）

        [MaxLength(191)]
        public string CustomerId { get; private set; }                  // 买家ID（来自电商平台，字符串）
        public string? CustomerName { get; private set; }             // 买家昵称
        public string? CustomerAvatar { get; private set; }           // 买家头像
        public Guid ShopId { get; private set; }                      // 所属店铺
        public string Platform { get; private set; } = string.Empty;  // TAOBAO, JD, DOUYIN, OTHER

        // 会话状态
        public SessionStatus Status { get; private set; } = SessionStatus.Pending;
        public SessionPriority Priority { get; private set; } = SessionPriority.Normal;

        // 分配信息
        public Guid? AssignedAgentId { get; private set; }            // 分配的客服ID
        public DateTime? AssignedAt { get; private set; }             // 分配时间
        public DateTime? ResolvedAt { get; private set; }             // 解决时间

        // 客户满意度（解决后评分 1-5）
        public byte? Satisfaction { get; private set; }

        // 统计
        public int MessageCount { get; private set; } = 0;            // 消息总数
        public int AiMessageCount { get; private set; } = 0;          // AI回复数
        public int AgentMessageCount { get; private set; } = 0;       // 人工回复数
        public TimeSpan? ResponseTime { get; private set; }           // 首次响应时长（从Pending到Active）
        public TimeSpan? ResolutionTime { get; private set; }         // 解决时长（从Active到Resolved）

        // 时间戳
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        // 导航属性
        public Seller? Shop { get; private set; }
        public Agent? AssignedAgent { get; private set; }
        public ICollection<ChatMessage> Messages { get; private set; } = new List<ChatMessage>();

        private ChatSession() { }

        /// <summary>
        /// 创建新会话（买家发起咨询时）
        /// </summary>
        public static ChatSession Create(
            Guid shopId,
            string platform,
            string customerId,
            string? customerName = null,
            string? customerAvatar = null)
        {
            return new ChatSession
            {
                Id = Guid.NewGuid(),
                SessionId = GenerateSessionId(),
                ShopId = shopId,
                Platform = platform,
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerAvatar = customerAvatar,
                Status = SessionStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 生成业务ID（格式：CS-YYYYMMDD-HHMMSS-随机4位）
        /// </summary>
        private static string GenerateSessionId()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var random = new Random().Next(1000, 9999);
            return $"CS-{timestamp}-{random}";
        }

        /// <summary>
        /// 客服接管会话
        /// </summary>
        public void AssignToAgent(Guid? agentId)
        {
            if (Status != SessionStatus.Pending)
                throw new InvalidOperationException("只能处理订阅中的会话");

            AssignedAgentId = agentId;
            AssignedAt = DateTime.UtcNow;
            Status = SessionStatus.Active;
            LastActiveAt = DateTime.UtcNow;

            // 计算响应时长（从创建到分配）
            if (AssignedAt.HasValue)
            {
                ResponseTime = AssignedAt.Value - CreatedAt;
            }
        }

        public void AddUserMessage()
        {
            MessageCount++;
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 客服回复消息
        /// </summary>
        public void AddAgentMessage()
        {
            if (Status != SessionStatus.Active)
                throw new InvalidOperationException("会话非活跃状态，无法回复");

            AgentMessageCount++;
            MessageCount++;
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// AI回复消息（用于统计）
        /// </summary>
        public void AddAiMessage()
        {
            AiMessageCount++;
            MessageCount++;
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 标记会话已解决
        /// </summary>
        public void Resolve(byte? satisfaction = null)
        {
            if (Status != SessionStatus.Active)
                throw new InvalidOperationException("只能解决活跃的会话");

            ResolvedAt = DateTime.UtcNow;
            Status = SessionStatus.Resolved;
            if (satisfaction.HasValue)
            {
                Satisfaction = satisfaction.Value;
            }

            // 计算解决时长
            if (AssignedAt.HasValue)
            {
                ResolutionTime = ResolvedAt - AssignedAt.Value;
            }
        }

        /// <summary>
        /// 更新满意度（质检评分）
        /// </summary>
        public void SetSatisfaction(byte satisfaction)
        {
            if (satisfaction < 1 || satisfaction > 5)
                throw new ArgumentException("Satisfaction must be between 1 and 5");

            Satisfaction = satisfaction;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 关闭会话（解决后或超时自动关闭）
        /// </summary>
        public void Close()
        {
            if (Status == SessionStatus.Resolved || Status == SessionStatus.Active)
            {
                Status = SessionStatus.Closed;
                LastActiveAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 重新打开会话（需要重新处理）
        /// </summary>
        public void Reopen()
        {
            Status = SessionStatus.Active;
            AssignedAt = DateTime.UtcNow;
            ResolvedAt = null;
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 提升优先级（VIP客户）
        /// </summary>
        public void ElevatePriority()
        {
            Priority = SessionPriority.High;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public enum SessionStatus
    {
        Pending = 1,     // 待分配（AI转人工后）
        Active = 2,      // 进行中（客服已接管）
        Resolved = 3,    // 已解决（客服标记）
        Closed = 4       // 已关闭（归档）
    }

    public enum SessionPriority
    {
        Normal = 1,
        High = 2,       // VIP客户或紧急问题
        Urgent = 3      // 投诉类
    }
}
