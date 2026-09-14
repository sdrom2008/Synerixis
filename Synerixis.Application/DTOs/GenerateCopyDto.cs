using System;

namespace Synerixis.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object for generating marketing copy.
    /// </summary>
    public class GenerateCopyDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string ToneOfVoice { get; set; } = string.Empty; // e.g., "Professional", "Witty", "Sales-oriented"
        /// <summary>可选：用于 AiUsageLog 记账</summary>
        public string? SellerId { get; set; }
    }
}
