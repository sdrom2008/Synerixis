using System;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// AI 调用用量记账（token / 估算费用）。无真实 usage 时按文本长度粗估并标记 IsEstimated。
    /// </summary>
    public class AiUsageLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>商家 Seller.Id（= ChatSession.ShopId）</summary>
        public Guid SellerId { get; set; }

        public Guid? SessionId { get; set; }

        /// <summary>模型名，如 qwen-max</summary>
        public string Model { get; set; } = string.Empty;

        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }

        /// <summary>粗估 USD；无 token 时为 0</summary>
        public decimal EstimatedCostUsd { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>draft | classify | order | logistics | competitor | product | chat | marketing | other</summary>
        public string Purpose { get; set; } = AiUsagePurposes.Other;

        /// <summary>true=Metadata 无 Usage，按 chars/4 估算</summary>
        public bool IsEstimated { get; set; }
    }

    public static class AiUsagePurposes
    {
        public const string Draft = "draft";
        public const string Classify = "classify";
        public const string Chat = "chat";
        public const string Marketing = "marketing";
        public const string Order = "order";
        public const string Logistics = "logistics";
        public const string Competitor = "competitor";
        public const string Product = "product";
        public const string Other = "other";
    }
}
