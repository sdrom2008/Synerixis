using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    public class SellerConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? SellerId { get; set; }                  // FK to Sellers.Id

        public string ShopName { get; set; } = string.Empty;        // 店铺名称
        public string ShopLogo { get; set; } = string.Empty;        // logo URL
        public string MainCategory { get; set; } = string.Empty;    // 主营类目（可多选逗号分隔）
        public string TargetCustomerDesc { get; set; } = string.Empty; // 目标客户群体描述

        // AI 偏好设置（影响 prompt）
        public string DefaultReplyTone { get; set; } = "professional"; // professional / friendly / humorous / concise
        public string PreferredLanguage { get; set; } = "zh";          // zh / en / bilingual
        public bool EnableAutoMarketingReminder { get; set; } = true;  // 是否开启主动营销提醒
        public int MemoryRetentionDays { get; set; } = 180;            // 记忆保留天数，0=永久

        /// <summary>Webhook 入站是否自动 AI 回复；false 时仅落库不 SendReply</summary>
        public bool EnableAutoReply { get; set; } = true;

        /// <summary>营业开始时间 HH:mm（本地业务约定，存字符串）</summary>
        public string BusinessHoursStart { get; set; } = "09:00";

        /// <summary>营业结束时间 HH:mm</summary>
        public string BusinessHoursEnd { get; set; } = "22:00";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        public Seller Seller { get; set; } = null!;
    }
}
