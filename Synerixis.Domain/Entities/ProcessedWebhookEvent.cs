using System;
using System.ComponentModel.DataAnnotations;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// Webhook 幂等落库：Platform + EventKey 唯一，处理前 try-insert，冲突即 duplicate。
    /// </summary>
    public class ProcessedWebhookEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(32)]
        public string Platform { get; set; } = string.Empty;

        /// <summary>MsgId，或空 MsgId 时的弱键 hash。</summary>
        [MaxLength(191)]
        public string EventKey { get; set; } = string.Empty;

        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>true = MsgId 缺失时用内容哈希生成的弱键。</summary>
        public bool IsWeakKey { get; set; }
    }
}
