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
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid ChatSessionId { get; private set; }  // 外键，指向 ChatSession
        public ChatSession? ChatSession { get; private set; }  // 导航属性

        // 映射到数据库的 SenderType 字段 (1=Customer, 2=Agent, 3=System)
        public int SenderType { get; private set; }
        public Guid? SenderId { get; private set; }  // 可选，AgentId 当 SenderType=2

        public string Content { get; private set; } = string.Empty;
        public int MessageType { get; private set; } = 1  // 1=Text, 2=Image, etc.
        public string? Metadata { get; private set; }
        public bool IsRead { get; private set; } = false;
        public DateTime? ReadAt { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

        private ChatMessage() { }

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
