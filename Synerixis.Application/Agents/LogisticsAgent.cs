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
    /// 物流查询 Agent。优先 Shopee get_tracking_info 真实轨迹；
    /// 无 API / 无权限时返回运单号+订单状态，禁止编造 checkpoint。
    /// </summary>
    public class LogisticsAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.LogisticsQuery;

        private static readonly Regex TrackingRegex = new(
            @"\b(?:SF|YT|JT|ZT|ST|YD|HT|EMS|CP)?[A-Z0-9]{8,22}\b|\b\d{10,18}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IECommercePlatformClient _platformClient;
        private readonly IOrderRepository? _orderRepository;
        private readonly ILlmClient? _llmClient;
        private readonly IPlatformClientRouter? _platformRouter;

        public LogisticsAgent(
            IECommercePlatformClient platformClient,
            IOrderRepository? orderRepository = null,
            ILlmClient? llmClient = null,
            IPlatformClientRouter? platformRouter = null)
        {
            _platformClient = platformClient;
            _orderRepository = orderRepository;
            _llmClient = llmClient;
            _platformRouter = platformRouter;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var (trackingId, order, source) = await ResolveTrackingAsync(userInput, context);

            string responseMessage;
            object? logisticsPayload = null;

            if (string.IsNullOrWhiteSpace(trackingId) && order == null)
            {
                responseMessage =
                    "抱歉，未能从对话或订单中找到运单号。请提供快递单号（或确认订单已发货），我再帮您查物流。";
            }
            else if (!string.IsNullOrWhiteSpace(trackingId))
            {
                PlatformTrackingInfoDto? trackingInfo = null;
                var orderSn = order?.OrderNo;
                var platform = context.Platform ?? order?.Platform ?? string.Empty;

                // 有 order_sn + tracking → 平台真实轨迹（Shopee get_tracking_info）
                if (_platformRouter != null
                    && !string.IsNullOrWhiteSpace(orderSn)
                    && !string.IsNullOrWhiteSpace(platform)
                    && _platformRouter.IsSupported(platform))
                {
                    try
                    {
                        var client = _platformRouter.GetClient(platform);
                        trackingInfo = await client.GetTrackingInfoAsync(
                            orderSn!,
                            trackingId,
                            context.PlatformShopId,
                            order?.Status);
                    }
                    catch
                    {
                        trackingInfo = null;
                    }
                }

                if (trackingInfo != null && trackingInfo.HasTrajectory)
                {
                    var latest = trackingInfo.Checkpoints
                        .OrderByDescending(c => c.Time ?? DateTime.MinValue)
                        .First();
                    var statusLabel = trackingInfo.LogisticsStatus ?? latest.Status ?? "运输中";
                    responseMessage =
                        $"您好，运单号【{trackingInfo.TrackingNumber ?? trackingId}】物流状态：【{statusLabel}】。" +
                        $"最新：{latest.Description ?? statusLabel}" +
                        (latest.Time.HasValue ? $"（{latest.Time:yyyy-MM-dd HH:mm} UTC）" : "") + "。";
                    if (order != null)
                        responseMessage += $" 关联订单 {order.OrderNo} 状态：{order.Status}。";
                    logisticsPayload = trackingInfo;
                }
                else
                {
                    // 尝试旧 IECommercePlatformClient（占位，通常 null）
                    LogisticsDetailsDto? logisticsDetails = null;
                    try
                    {
                        logisticsDetails = await _platformClient.GetLogisticsDetailsAsync(
                            platform, trackingId);
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
                        var company = order?.LogisticsCompany;
                        var orderStatus = trackingInfo?.OrderStatus ?? order?.Status;
                        var parts = new List<string>
                        {
                            $"已解析运单号：【{trackingInfo?.TrackingNumber ?? trackingId}】"
                        };
                        if (!string.IsNullOrWhiteSpace(company))
                            parts.Add($"承运商：{company}");
                        if (!string.IsNullOrWhiteSpace(orderStatus))
                            parts.Add($"订单状态：{orderStatus}");
                        if (order != null && !string.IsNullOrWhiteSpace(order.OrderNo))
                            parts.Add($"订单号：{order.OrderNo}");
                        parts.Add("仅有运单号/订单状态，轨迹暂不可用");
                        responseMessage = string.Join("；", parts) + "。";
                        logisticsPayload = trackingInfo ?? new PlatformTrackingInfoDto
                        {
                            TrackingNumber = trackingId,
                            OrderStatus = orderStatus,
                            Checkpoints = new List<TrackingCheckpointDto>(),
                            Warning = "tracking_unavailable",
                            Message = "仅有运单号/订单状态，轨迹暂不可用"
                        };
                    }
                }
            }
            else
            {
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
                        source,
                        logistics = logisticsPayload
                    }
                }
            };

            return new AgentProcessResult(messages, Success: true);
        }

        private async Task<(string? trackingId, Order? order, string source)> ResolveTrackingAsync(
            string userInput, ChatContext context)
        {
            var fromInput = ExtractTracking(userInput);
            if (!string.IsNullOrWhiteSpace(fromInput))
            {
                var order = await FindOrderByTrackingOrCustomerAsync(context, fromInput);
                return (fromInput, order, "message");
            }

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

        internal static string? ExtractTracking(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            foreach (Match m in TrackingRegex.Matches(text))
            {
                var v = m.Value.Trim();
                if (v.Length < 8)
                    continue;
                if (v.StartsWith("SIMULATED", StringComparison.OrdinalIgnoreCase))
                    continue;
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
            if (v.All(char.IsLetter) && v.Length < 12)
                return true;
            return false;
        }
    }
}
