using Synerixis.Application.Interfaces;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// 订单查询 Agent
    /// 支持两种模式：
    /// 1. B2B 模式（商家自助）：查询自己店铺的订单列表
    /// 2. B2C 模式（AI 客服）：查询买家在店铺的订单
    /// </summary>
    public class OrderAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.QueryOrder;

        private readonly IOrderRepository _orderRepository;

        public OrderAgent(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            // 1. 场景判定：根据 ChatContext 判断是商家查账 (B2B) 还是买家咨询 (B2C)
            // 如果 CustomerId (买家 ID) 存在，则是 B2C 场景
            // 如果 CustomerId 为空，但 ShopId 存在，则是 B2B 场景（商家自助/后台）

            if (context.ShopId == Guid.Empty && context.SellerId == null)
            {
                return new AgentProcessResult(
                    Messages: Array.Empty<ChatMessageDto>(),
                    Success: false,
                    ErrorMessage: "缺少上下文信息，无法查询订单");
            }

            var shopId = context.ShopId;
            var customerId = context.CustomerId;

            // --- 路径 A: B2C 模式 (AI 代查) ---
            // 场景：Shopee/TikTok 买家在聊天窗口问 "我的包裹到哪了"
            if (!string.IsNullOrEmpty(customerId))
            {
                var orders = await _orderRepository.GetByShopIdAndCustomerIdAsync(shopId, customerId);
                
                if (!orders.Any())
                {
                    return new AgentProcessResult(
                        Messages: new List<ChatMessageDto>
                        {
                            new ChatMessageDto 
                            { 
                                IsFromUser = false, 
                                Content = "亲，查了一下，您在这个店铺还没有订单记录哦。", 
                                MessageType = "text",
                                Data = new { reason = "NoOrders" }
                            }
                        },
                        Success: true);
                }

                var recentOrder = orders.First();
                var response = FormatBuyerResponse(recentOrder);
                
                var messages = new List<ChatMessageDto>
                {
                    new ChatMessageDto 
                    { 
                        IsFromUser = false, 
                        Content = response, 
                        MessageType = "text",
                        Data = new { orderId = recentOrder.OrderNo, status = recentOrder.Status }
                    }
                };
                return new AgentProcessResult(messages, Success: true);
            }

            // --- 路径 B: B2B 模式 (商家自助) ---
            // 场景：商家在管理后台或小程序问 "今天有哪些待发货"
            var paginatedOrders = await _orderRepository.GetByShopIdAsync(shopId);

            if (paginatedOrders.TotalCount == 0)
            {
                return new AgentProcessResult(
                    Messages: new List<ChatMessageDto>
                    {
                        new ChatMessageDto 
                        { 
                            IsFromUser = false, 
                            Content = "当前店铺暂无订单数据。", 
                            MessageType = "text",
                            Data = new { reason = "NoOrders" }
                        }
                    },
                    Success: true);
            }

            var summary = FormatMerchantSummary(paginatedOrders);
            var merchantMessages = new List<ChatMessageDto>
            {
                new ChatMessageDto 
                { 
                    IsFromUser = false, 
                    Content = summary, 
                    MessageType = "text",
                    Data = new { total = paginatedOrders.TotalCount, page = paginatedOrders.Page }
                }
            };
            return new AgentProcessResult(merchantMessages, Success: true);
        }

        private string FormatBuyerResponse(Order order)
        {
            // 针对买家的语气
            var statusText = MapStatus(order.Status);
            var logisticsInfo = string.IsNullOrEmpty(order.LogisticsNo) 
                ? "正在处理中，尚未发货" 
                : $"快递公司：{order.LogisticsCompany}，单号：{order.LogisticsNo}";

            return $"亲，您好！\n\n查询到您的最新订单【{order.OrderNo}】状态为：{statusText}。\n\n物流信息：{logisticsInfo}\n下单时间：{order.OrderTime:yyyy-MM-dd HH:mm}";
        }

        private string FormatMerchantSummary(PaginatedList<Order> list)
        {
            // 针对商家的语气（数据导向）
            var summaryBuilder = new System.Text.StringBuilder();
            summaryBuilder.AppendLine($"📊 订单查询结果（共 {list.TotalCount} 条）：");
            
            foreach (var order in list.Items.Take(5)) // 先展示前5条
            {
                summaryBuilder.AppendLine($"- [{order.OrderNo}] 金额: {order.TotalAmount} | 状态: {MapStatus(order.Status)}");
            }
            
            if (list.TotalCount > 5)
            {
                summaryBuilder.AppendLine($"... 以及更多 {list.TotalCount - 5} 条订单");
            }
            
            return summaryBuilder.ToString();
        }

        private string MapStatus(string status)
        {
            return status switch
            {
                "PendingPayment" => "待付款",
                "Paid" => "已付款",
                "Shipped" => "已发货",
                "Received" => "已签收",
                "Completed" => "已完成",
                "Cancelled" => "已取消",
                "Refunded" => "已退款",
                _ => status
            };
        }
    }
}
