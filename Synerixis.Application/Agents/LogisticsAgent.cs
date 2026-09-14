using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Synerixis.Application.Helpers;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Application.Interfaces.Infrastructure;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// 物流查询 Agent。从会话消息 / 关联 Order.LogisticsNo 解析运单号；
    /// 无真实承运商 API 时返回「已解析运单号 + 订单状态」，禁止编造固定模拟运单号。
    /// </summary>
    public class LogisticsAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.LogisticsQuery;

        /// <summary>
        /// 常见快递单号：SF/YT/JT/ZTO 等前缀 + 数字，或 10–18 位纯数字 / 8–20 位字母数字。
        /// </summary>
        private static readonly Regex TrackingRegex = new(
            @"\b(?:SF|YT|JT|ZT|ST|YD|HT|EMS|CP)?[A-Z0-9]{8,22}\b|\b\d{10,18}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IECommercePlatformClient _platformClient;
        private readonly IOrderRepository? _orderRepository;
        private readonly ILlmClient? _llmClient;

        public LogisticsAgent(
            IECommercePlatformClient platformClient,
            IOrderRepository? orderRepository = null,
            ILlmClient? llmClient = null)
        {
            _platformClient = platformClient;
            _orderRepository = orderRepository;
            _llmClient = llmClient;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var (trackingId, order, source) = await ResolveTrackingAsync(userInput, context);

            string responseMessage;
            if (string.IsNullOrWhiteSpace(trackingId) && order == null)
            {
                responseMessage =
                    "抱歉，未能从对话或订单中找到运单号。请提供快递单号（或确认订单已发货），我再帮您查物流。";
            }
            else if (!string.IsNullOrWhiteSpace(trackingId))
            {
                // 尝试平台客户端；模拟实现仅对特定假号有结果，真实环境无承运商 API 时忽略假数据
                LogisticsDetailsDto? logisticsDetails = null;
                try
                {
                    logisticsDetails = await _platformClient.GetLogisticsDetailsAsync(
                        context.Platform ?? string.Empty, trackingId);
                    // 拒绝把「含 67890 的模拟命中」当成真实承运商结果
                    if (logisticsDetails != null &&
                        (trackingId.Contains("67890", StringComparison.Ordinal) ||
                         trackingId.StartsWith("SIMULATED", StringComparison.OrdinalIgnoreCase)))
                    {
                        logisticsDetails = null;
                    }
                }
                catch
                {
                    logisticsDetails = null;
                }

                if (logisticsDetails != null)
                {
                    responseMessage =
                        $"您好，运单号【{trackingId}】最新状态：【{logisticsDetails.Status}】。当前位置：{logisticsDetails.CurrentLocation}。";
                    if (order != null)
                        responseMessage += $" 关联订单 {order.OrderNo} 状态：{order.Status}。";
                }
                else
                {
                    // 无真实承运商 API：诚实返回已解析运单号 + 订单状态
                    var company = order?.LogisticsCompany;
                    var orderStatus = order?.Status;
                    var parts = new List<string>
                    {
                        $"已解析运单号：【{trackingId}】"
                    };
                    if (!string.IsNullOrWhiteSpace(company))
                        parts.Add($"承运商：{company}");
                    if (!string.IsNullOrWhiteSpace(orderStatus))
                        parts.Add($"订单状态：{orderStatus}");
                    if (order != null && !string.IsNullOrWhiteSpace(order.OrderNo))
                        parts.Add($"订单号：{order.OrderNo}");
                    parts.Add("（暂未接通真实承运商轨迹 API，以上为订单侧信息，非编造运单）");
                    responseMessage = string.Join("；", parts) + "。";
                }
            }
            else
            {
                // 有订单但无 LogisticsNo
                responseMessage =
                    $"查到您的订单【{order!.OrderNo}】当前状态为【{order.Status}】，但尚未登记快递单号。发货后会有运单信息，您也可直接发快递单号给我查询。";
            }

            if (_llmClient != null)
            {
                try
                {
                    var prompt = $"""
你是跨境电商客服，根据以下物流事实用口语简短回复买家（中文，不超过 80 字，不要编造运单号或轨迹）：
用户问：{userInput}
事实：{responseMessage}
来源：{source}
""";
                    var usage = LlmUsageHelper.FromChatContext(context, AiUsagePurposes.Logistics);
                    var polished = await _llmClient.GenerateTextAsync(prompt, usage);
                    if (!string.IsNullOrWhiteSpace(polished))
                        responseMessage = polished.Trim();
                }
                catch
                {
                    // 保持模板话术
                }
            }

            var messages = new List<ChatMessageDto>
            {
                new ChatMessageDto
                {
                    IsFromUser = false,
                    Content = responseMessage,
                    MessageType = "text",
                    Timestamp = DateTime.UtcNow,
                    Data = new
                    {
                        trackingId,
                        orderNo = order?.OrderNo,
                        orderStatus = order?.Status,
                        source
                    }
                }
            };

            return new AgentProcessResult(messages, Success: true);
        }

        private async Task<(string? trackingId, Order? order, string source)> ResolveTrackingAsync(
            string userInput, ChatContext context)
        {
            // 1) 用户当前消息中的运单号
            var fromInput = ExtractTracking(userInput);
            if (!string.IsNullOrWhiteSpace(fromInput))
            {
                var order = await FindOrderByTrackingOrCustomerAsync(context, fromInput);
                return (fromInput, order, "message");
            }

            // 2) 会话历史消息
            if (context.Messages != null)
            {
                foreach (var m in context.Messages.AsEnumerable().Reverse())
                {
                    var t = ExtractTracking(m.Content);
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        var order = await FindOrderByTrackingOrCustomerAsync(context, t);
                        return (t, order, "history");
                    }
                }
            }

            // 3) 关联订单 LogisticsNo（ShippingTracking 字段在本域为 LogisticsNo）
            if (_orderRepository != null && context.ShopId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(context.CustomerId))
            {
                var orders = (await _orderRepository.GetByShopIdAndCustomerIdAsync(
                    context.ShopId, context.CustomerId)).ToList();
                var withLogistics = orders.FirstOrDefault(o => !string.IsNullOrWhiteSpace(o.LogisticsNo));
                if (withLogistics != null)
                    return (withLogistics.LogisticsNo, withLogistics, "order.LogisticsNo");

                var any = orders.FirstOrDefault();
                if (any != null)
                    return (null, any, "order.noTracking");
            }

            return (null, null, "none");
        }

        private async Task<Order?> FindOrderByTrackingOrCustomerAsync(ChatContext context, string trackingId)
        {
            if (_orderRepository == null || context.ShopId == Guid.Empty)
                return null;

            if (!string.IsNullOrWhiteSpace(context.CustomerId))
            {
                var orders = (await _orderRepository.GetByShopIdAndCustomerIdAsync(
                    context.ShopId, context.CustomerId)).ToList();
                var match = orders.FirstOrDefault(o =>
                    string.Equals(o.LogisticsNo, trackingId, StringComparison.OrdinalIgnoreCase));
                return match ?? orders.FirstOrDefault();
            }

            return null;
        }

        /// <summary>从文本提取运单号；过滤过短/明显非运单 token。</summary>
        internal static string? ExtractTracking(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            foreach (Match m in TrackingRegex.Matches(text))
            {
                var v = m.Value.Trim();
                if (v.Length < 8)
                    continue;
                // 排除纯中文语境里的年份等短数字已由长度约束；排除明显模拟号
                if (v.StartsWith("SIMULATED", StringComparison.OrdinalIgnoreCase))
                    continue;
                // 排除常见非运单词
                if (IsLikelyNotTracking(v))
                    continue;
                return v.ToUpperInvariant();
            }

            return null;
        }

        private static bool IsLikelyNotTracking(string v)
        {
            var lower = v.ToLowerInvariant();
            if (lower is "tracking" or "shipment" or "logistics" or "ordernumber")
                return true;
            // 纯字母短词
            if (v.All(char.IsLetter) && v.Length < 12)
                return true;
            return false;
        }
    }
}
