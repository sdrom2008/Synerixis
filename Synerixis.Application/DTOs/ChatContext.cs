using System;
using System.Collections.Generic;

namespace Synerixis.Application.DTOs
{
    public class ChatContext
    {
        public string ConversationId { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // 所属平台 (e.g., "taobao", "douyin")
        public List<ChatMessageDto> Messages { get; set; } = new();
        // 可选扩展：Dictionary<string, object> Variables; // 上下文变量
    }
}
