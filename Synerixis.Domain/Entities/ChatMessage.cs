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
        public Guid ConversationId { get; private set; }  // 外键，必须保留
        public Conversation? Conversation { get; private set; }  // 导航，可选

        public bool IsFromUser { get; private set; }
        public string Content { get; private set; } = string.Empty;

        // 推荐改成 MessageType（更规范，与 DTO 一致）
        public string MessageType { get; private set; } = "text";  // 改名：Type → MessageType

        public string? DataJson { get; private set; }
        public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

        private ChatMessage() { }

        public static ChatMessage FromUser(string content, Guid conversationId)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,  // 手动设置
                IsFromUser = true,
                Content = content,
                MessageType = "text"  // 显式设置
            };
            Console.WriteLine("创建 user 消息 Id: " + msg.Id);
            return msg;
        }

        public static ChatMessage FromAI(string content, string messageType = "text", object? data = null,Guid conversationId = default)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                IsFromUser = false,
                Content = content,
                MessageType = messageType,
                DataJson = data != null ? System.Text.Json.JsonSerializer.Serialize(data) : null
            };
            Console.WriteLine("创建1 AI 消息 Id: " + msg.Id);
            return msg;
        }

        public static ChatMessage FromAI(string content, string messageType = "text", object? data = null, DateTime? timestamp = null)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                IsFromUser = false,
                Content = content,
                MessageType = messageType,
                DataJson = data != null ? JsonConvert.SerializeObject(data) : null, // 改用 Newtonsoft
                Timestamp = timestamp ?? DateTime.UtcNow
            };
            Console.WriteLine("创建2 AI 消息 Id: " + msg.Id);
            return msg;
        }

        /// <summary>
        /// 创建客服回复消息
        /// </summary>
        public static ChatMessage FromAgent(string content, Guid conversationId)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                IsFromUser = false,
                Content = content,
                MessageType = "text"
            };
            Console.WriteLine("创建 agent 消息 Id: " + msg.Id);
            return msg;
        }
    }
}
