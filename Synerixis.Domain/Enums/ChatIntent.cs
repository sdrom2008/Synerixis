namespace Synerixis.Domain.Enums
{
    /// <summary>
    /// 对话意图。权威名称与 IntentClassifier 输出一致；旧别名保留同数值兼容。
    /// </summary>
    public enum ChatIntent
    {
        Unknown = 0,
        GeneralChat = 1,              // 普通闲聊
        OrderQuery = 2,               // 权威：订单查询（IntentClassifier / OrderAgent）
        QueryOrder = OrderQuery,      // 别名：兼容旧代码
        LogisticsQuery = 3,           // 权威：物流查询
        QueryLogistics = LogisticsQuery, // 别名：兼容旧代码
        ProductOptimization = 4,      // 商品优化
        Appointment = 5,
        AfterSale = 6,
        MarketingFollowup = 7,
        CompetitorAnalysis = 100,     // 竞品分析
    }

    public record ChatMessage(
    bool IsFromUser,
    string Content,
    string MessageType = "text",
    object? Data = null,
    DateTime Timestamp = default)
    {
        public ChatMessage() : this(false, string.Empty) { }
    }
}
