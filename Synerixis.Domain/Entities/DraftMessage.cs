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

        /// <summary>Pending / Sent / Discarded / Superseded</summary>
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
        /// <summary>转人工后旧草稿标记；仍可人工发送，但不作为「最新待审」优先</summary>
        public const string Superseded = "Superseded";

        /// <summary>人审可操作的草稿（含转人工后保留的旧草稿）</summary>
        public static bool IsActionable(string? status) =>
            string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, Superseded, StringComparison.OrdinalIgnoreCase);
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
