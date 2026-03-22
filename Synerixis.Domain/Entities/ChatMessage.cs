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

        public bool IsFromUser { get; private set; }
        public string Content { get; private set; } = string.Empty;
        public string MessageType { get; private set; } = "text";
        public string? DataJson { get; private set; }
        public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

        private ChatMessage() { }

        public static ChatMessage FromUser(string content, Guid chatSessionId)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = chatSessionId,
                IsFromUser = true,
                Content = content,
                MessageType = "text"
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
                IsFromUser = false,
                Content = content,
                MessageType = messageType,
                DataJson = data != null ? System.Text.Json.JsonSerializer.Serialize(data) : null
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
                IsFromUser = false,
                Content = content,
                MessageType = "text"
            };
            Console.WriteLine("创建 agent 消息 Id: " + msg.Id);
            return msg;
        }
    }
}
