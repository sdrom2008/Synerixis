using System;

namespace Synerixis.Application.Interfaces.Ai
{
    /// <summary>LLM 调用的用量记账上下文（Seller / Purpose）。</summary>
    public class LlmCallContext
    {
        public Guid SellerId { get; set; }
        public Guid? SessionId { get; set; }
        public string Purpose { get; set; } = "other";
        public string? Model { get; set; }
    }
}
