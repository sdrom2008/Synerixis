using System;

namespace Synerixis.Application.DTOs
{
    public class SellerConfigUpdateDto
    {
        public string? ShopName { get; set; }
        public string? ShopLogo { get; set; }
        public string? MainCategory { get; set; }
        public string? TargetCustomerDesc { get; set; }
        public string? DefaultReplyTone { get; set; }
        public string? PreferredLanguage { get; set; }
        public bool? EnableAutoMarketingReminder { get; set; }
        public int? MemoryRetentionDays { get; set; }
        public bool? EnableAutoReply { get; set; }
        public string? BusinessHoursStart { get; set; }
        public string? BusinessHoursEnd { get; set; }
        /// <summary>DraftFirst | AutoSend</summary>
        public string? OutboundMode { get; set; }
    }
}
