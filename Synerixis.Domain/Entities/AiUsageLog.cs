using System;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// AI 调用用量记账（token / 估算费用）。无真实 usage 时记 0。
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

        /// <summary>draft | classify | chat | marketing | other</summary>
        public string Purpose { get; set; } = AiUsagePurposes.Other;
    }

    public static class AiUsagePurposes
    {
        public const string Draft = "draft";
        public const string Classify = "classify";
        public const string Chat = "chat";
        public const string Marketing = "marketing";
        public const string Other = "other";
    }
}
