using System;
using System.Collections.Generic;

namespace Synerixis.Application.DTOs
{
    public class ChatMessageReplyDto
    {
        public Guid ConversationId { get; set; }
        public Guid MessageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string MessageType { get; set; } = "text";
        public object? Data { get; set; }
    }
}
