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
        /// <summary>坐席工作语（默认 ZH）。API 亦作 workingLanguage；历史字段 PreferredLanguage。</summary>
        public string PreferredLanguage { get; set; } = "zh";          // ZH / EN / ID / TH / VN

        /// <summary>本店支持的买家语种 CSV，优先级 ID,TH,VN,EN,ZH。</summary>
        public string SupportedLanguages { get; set; } = "ID,TH,VN,EN,ZH";

        public bool EnableAutoMarketingReminder { get; set; } = true;  // 是否开启主动营销提醒
        public int MemoryRetentionDays { get; set; } = 180;            // 记忆保留天数，0=永久

        /// <summary>Webhook 入站是否生成 AI 草稿/回复；false 时仅落库买家消息</summary>
        public bool EnableAutoReply { get; set; } = true;

        /// <summary>
        /// 出站模式：DraftFirst（默认，人审后发送）| AutoSend（显式开启才自动 SendReply，有合规风险）
        /// </summary>
        public string OutboundMode { get; set; } = OutboundModes.DraftFirst;

        /// <summary>营业开始时间 HH:mm（本地业务约定，存字符串）</summary>
        public string BusinessHoursStart { get; set; } = "09:00";

        /// <summary>营业结束时间 HH:mm</summary>
        public string BusinessHoursEnd { get; set; } = "22:00";

        /// <summary>建议回复 SLA 小时数（needsResponseBy = 买家消息 + 该值；默认 12）</summary>
        public int ResponseSlaHours { get; set; } = 12;

        /// <summary>告警阈值小时数，逗号分隔，如 1,3,12（用于 /api/merchant/alerts）</summary>
        public string AlertThresholdHours { get; set; } = "1,3,12";

        /// <summary>低置信度时自动转人工（默认开启）</summary>
        public bool AutoHandoffOnLowConfidence { get; set; } = true;

        /// <summary>分类置信度低于此阈值则转人工（0~1，默认 0.45）</summary>
        public double HandoffConfidenceThreshold { get; set; } = 0.45;

        /// <summary>敏感词（逗号分隔）；命中则转人工且不生成新草稿</summary>
        public string SensitiveKeywords { get; set; } =
            "退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼";

        /// <summary>营业时间外是否直接 PendingHumanHandoff（默认 true）</summary>
        public bool HandoffOutsideBusinessHours { get; set; } = true;

        /// <summary>店铺本地时区（IANA，默认 Asia/Shanghai）</summary>
        public string TimeZoneId { get; set; } = "Asia/Shanghai";

        /// <summary>
        /// 商家级 LLM API Key（DashScope / 通义兼容）。优先于平台 Llm:ApiKey。
        /// 空 = 使用平台配置；未配置任何 Key 时入站走规则草稿降级。
        /// </summary>
        public string? LlmApiKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        public Seller Seller { get; set; } = null!;
    }
}
