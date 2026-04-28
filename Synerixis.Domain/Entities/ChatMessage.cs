using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    public class ChatMessage
    {
        private ChatMessage() { }

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ChatSessionId { get; set; }
        public ChatSession? ChatSession { get; set; }

        // 映射到数据库的 SenderType 字段 (1=Customer, 2=Agent, 3=System)
        public int SenderType { get; set; }
        public Guid? SenderId { get; set; }  // 可选，AgentId 当 SenderType=2

        public string Content { get; set; } = string.Empty;
        public int MessageType { get; set; } = 1;  // 1=Text, 2=Image, etc.
        public string? Metadata { get; set; }

        /// <summary>
        /// 平台消息唯一 ID（用于 Webhook 幂等性去重）
        /// 如 TikTok event_id, Shopee push_id 等
        /// </summary>
        public string? PlatformMsgId { get; set; }

        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 来源类型（Customer/SenderType映射）
        /// </summary>
        public string SenderTypeName => SenderType switch
        {
            1 => "Customer",
            2 => "Agent",
            3 => "System",
            _ => "Unknown"
        };
        
        /// <summary>
        /// 是否为买家发送
        /// </summary>
        public bool IsFromUser => SenderType == 1;
        
        /// <summary>
        /// 消息时间戳（别名）
        /// </summary>
        public DateTime Timestamp => CreatedAt;

        public static ChatMessage FromUser(string content, Guid chatSessionId)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                SenderType = 1,  // Customer
                Content = content,
                MessageType = 1,  // Text
                CreatedAt = DateTime.UtcNow
            };
            Console.WriteLine("创建 user 消息 Id: " + msg.Id);
            return msg;
        }

        public static ChatMessage FromAI(string content, string messageType = "text", object? data = null, Guid chatSessionId = default)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                SenderType = 3,  // System/AI
                Content = content,
                MessageType = messageType == "text" ? 1 : 2,  // 简化处理，text=1, else=2
                Metadata = data != null ? System.Text.Json.JsonSerializer.Serialize(data) : null,
                CreatedAt = DateTime.UtcNow
            };
            Console.WriteLine("创建 AI 消息 Id: " + msg.Id);
            return msg;
        }

        public static ChatMessage FromAgent(string content, Guid chatSessionId)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                SenderType = 2,  // Agent
                Content = content,
                MessageType = 1,
                CreatedAt = DateTime.UtcNow
            };
            Console.WriteLine("创建 agent 消息 Id: " + msg.Id);
            return msg;
        }
    }
}
