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
    /// 2. B2C 模式（AI 客服）：查询买家在店铺的订单；本地库无数据时回源平台 API
    /// </summary>
    public class OrderAgent : IAgent
    {
        /// <summary>与 IntentClassifier 输出的 OrderQuery 对齐（QueryOrder 为同值别名）</summary>
        public ChatIntent SupportedIntent => ChatIntent.OrderQuery;

        private readonly IOrderRepository _orderRepository;
        private readonly IPlatformClientRouter? _platformClientRouter;

        /// <summary>
        /// platformClientRouter 可选：未注册平台 DI 时不崩溃，仅跳过平台回源。
        /// </summary>
        public OrderAgent(
            IOrderRepository orderRepository,
            IPlatformClientRouter? platformClientRouter = null)
        {
            _orderRepository = orderRepository;
            _platformClientRouter = platformClientRouter;
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
                    // DB 未命中 → 回源平台 GetCustomerOrderAsync
                    var platformSummary = await TryGetPlatformCustomerOrderAsync(
                        context.Platform, customerId, context.PlatformShopId);

                    if (!string.IsNullOrWhiteSpace(platformSummary))
                    {
                        var platformReply = FormatPlatformBuyerResponse(platformSummary!);
                        return new AgentProcessResult(
                            Messages: new List<ChatMessageDto>
                            {
                                new ChatMessageDto
                                {
                                    IsFromUser = false,
                                    Content = platformReply,
                                    MessageType = "text",
                                    Data = new { source = "platform", summary = platformSummary }
                                }
                            },
                            Success: true);
                    }

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

        /// <summary>
        /// 通过 Application 层 IPlatformClientRouter 回源查单；任何失败静默返回 null。
        /// </summary>
        private async Task<string?> TryGetPlatformCustomerOrderAsync(string? platform, string customerId, string? platformShopId = null)
        {
            if (_platformClientRouter == null)
                return null;

            if (string.IsNullOrWhiteSpace(platform))
                return null;

            try
            {
                if (!_platformClientRouter.IsSupported(platform))
                    return null;

                var client = _platformClientRouter.GetClient(platform);
                return await client.GetCustomerOrderAsync(platform, customerId, platformShopId);
            }
            catch
            {
                // 平台客户端未注册 / 配置缺失 / API 异常：不打断买家话术
                return null;
            }
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

        /// <summary>
        /// 将平台摘要（如 order_sn=..., status=..., tracking=...）转成买家可读话术。
        /// </summary>
        private string FormatPlatformBuyerResponse(string platformSummary)
        {
            string? orderSn = null;
            string? status = null;
            string? tracking = null;

            foreach (var part in platformSummary.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var key = part[..eq].Trim();
                var value = part[(eq + 1)..].Trim();
                if (key.Equals("order_sn", StringComparison.OrdinalIgnoreCase))
                    orderSn = value;
                else if (key.Equals("status", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("order_status", StringComparison.OrdinalIgnoreCase))
                    status = value;
                else if (key.Equals("tracking", StringComparison.OrdinalIgnoreCase) ||
                         key.Equals("tracking_number", StringComparison.OrdinalIgnoreCase))
                    tracking = value;
            }

            if (string.IsNullOrEmpty(orderSn) && string.IsNullOrEmpty(status))
            {
                return $"亲，您好！\n\n已从平台查到您的订单信息：\n{platformSummary}";
            }

            var statusText = MapStatus(status ?? "未知");
            var logisticsInfo = string.IsNullOrEmpty(tracking)
                ? "正在处理中，或暂无物流单号"
                : $"物流单号：{tracking}";

            return $"亲，您好！\n\n查询到您的订单【{orderSn ?? "未知"}】状态为：{statusText}。\n\n物流信息：{logisticsInfo}";
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
                // Shopee 常见状态
                "UNPAID" => "待付款",
                "READY_TO_SHIP" => "待发货",
                "PROCESSED" => "已处理",
                "SHIPPED" => "已发货",
                "COMPLETED" => "已完成",
                "IN_CANCEL" => "取消中",
                "CANCELLED" => "已取消",
                "TO_RETURN" => "退货中",
                _ => status
            };
        }
    }
}
