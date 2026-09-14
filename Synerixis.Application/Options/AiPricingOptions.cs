namespace Synerixis.Application.Options
{
    /// <summary>appsettings Ai:PricePer1kInput / PricePer1kOutput（USD per 1k tokens）。</summary>
    public class AiPricingOptions
    {
        public const string SectionName = "Ai";

        /// <summary>输入 token 单价（USD / 1k），默认约等于通义粗估。</summary>
        public decimal PricePer1kInput { get; set; } = 0.0004m;

        /// <summary>输出 token 单价（USD / 1k）。</summary>
        public decimal PricePer1kOutput { get; set; } = 0.0012m;
    }
}
