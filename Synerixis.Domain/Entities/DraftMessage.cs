using System;
using System.ComponentModel.DataAnnotations;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// AI 生成的待发送草稿（人审后出站）。默认产品路径，非自动 SendReply。
    /// </summary>
    public class DraftMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ChatSessionId { get; set; }
        public ChatSession? ChatSession { get; set; }

        public string Content { get; set; } = string.Empty;

        /// <summary>Pending / Sent / Discarded</summary>
        [MaxLength(32)]
        public string Status { get; set; } = DraftStatuses.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? SentAt { get; set; }

        /// <summary>发送成功后对应的 ChatMessage.Id</summary>
        public Guid? SentMessageId { get; set; }
    }

    public static class DraftStatuses
    {
        public const string Pending = "Pending";
        public const string Sent = "Sent";
        public const string Discarded = "Discarded";
    }

    /// <summary>出站模式：默认草稿优先，禁止默认自动 SendReply。</summary>
    public static class OutboundModes
    {
        public const string DraftFirst = "DraftFirst";
        public const string AutoSend = "AutoSend";

        public static bool IsAutoSend(string? mode) =>
            string.Equals(mode, AutoSend, StringComparison.OrdinalIgnoreCase);

        public static string Normalize(string? mode) =>
            IsAutoSend(mode) ? AutoSend : DraftFirst;
    }
}
