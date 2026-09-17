namespace Synerixis.Application.Helpers
{
    /// <summary>无 LLM Key 时的规则草稿（演示与降级路径）。</summary>
    public static class RuleBasedDraftHelper
    {
        public const string Prefix = "【未配置 AI·规则草稿】";

        public static string Build(string? userInput)
        {
            var body = LocalReply(userInput);
            return Prefix + body;
        }

        public static string LocalReply(string? userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                return "您好！您的消息已收到，我们会尽快为您处理。";

            var text = userInput.Trim();
            var lower = text.ToLowerInvariant();

            if (ContainsAny(text, "物流", "运单", "快递") || ContainsAny(lower, "tracking", "shipment"))
                return "您好！关于物流进度，请提供订单号或运单号，我们帮您查询。";

            if (ContainsAny(text, "订单", "查单", "退款进度") || ContainsAny(lower, "order"))
                return "您好！请提供订单号，我们马上帮您核对订单状态。";

            if (ContainsAny(text, "退款", "退货", "换货", "投诉"))
                return "您好！售后问题已记录，坐席将尽快与您确认处理方案。";

            if (ContainsAny(text, "你好", "在吗", "您好") || ContainsAny(lower, "hello", "hi"))
                return "您好！请问有什么可以帮您？";

            if (ContainsAny(text, "价格", "多少钱") || ContainsAny(lower, "price"))
                return "您好！具体价格以店铺页面为准；如需优惠或套装，请告知商品名称。";

            return "您好！您的消息已收到。当前店铺未配置 AI，已生成规则草稿供坐席人审发送。";
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (!string.IsNullOrEmpty(n) && haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
