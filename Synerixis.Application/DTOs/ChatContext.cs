using System;
using System.Collections.Generic;

namespace Synerixis.Application.DTOs
{
    public class ChatContext
    {
        public string ConversationId { get; set; } = string.Empty;
        public string SellerId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty; // 所属平台 (e.g., "taobao", "douyin")
        public string CustomerId { get; set; } = string.Empty; // 买家ID（电商平台唯一标识）
        public Guid ShopId { get; set; }
        /// <summary>平台侧店铺 ID（如 Shopee to_shop_id），用于 per-shop token / 查单</summary>
        public string? PlatformShopId { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new();

        // 商品相关上下文（用于优化、分析）
        public string? ProductName { get; set; }
        public string? ProductDescription { get; set; }
        public string? Category { get; set; }
        public List<string>? ImageUrls { get; set; }
        public string? TargetPlatform { get; set; } // 优化目标平台（可能不同于当前平台）
    }
}
