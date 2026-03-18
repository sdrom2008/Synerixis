using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Enums
{
    public enum ChatIntent
    {
        Unknown = 0,
        GeneralChat = 1,          // 普通闲聊
        OrderQuery = 2,
        QueryOrder = 2,           // 别名：兼容旧代码
        LogisticsQuery = 3,       // 新增：物流查询
        QueryLogistics = 3,       // 别名：兼容旧代码
        ProductOptimization = 4,  // 商品优化（已有）
        CompetitorAnalysis = 100, // 竞品分析（新增）
        Appointment = 5,
        AfterSale = 6,
        MarketingFollowup = 7
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
