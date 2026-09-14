using System.Collections.Generic;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Infrastructure;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// Agent responsible for handling logistics-related queries.
    /// </summary>
    public class LogisticsAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.LogisticsQuery;

        private readonly IECommercePlatformClient _platformClient;

        public LogisticsAgent(IECommercePlatformClient platformClient)
        {
            _platformClient = platformClient;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var trackingId = "SIMULATED_TRACKING_67890";

            var logisticsDetails = await _platformClient.GetLogisticsDetailsAsync(context.Platform, trackingId);

            string responseMessage;
            if (logisticsDetails != null)
            {
                responseMessage = $"您好，查询到运单号【{trackingId}】的最新状态是：【{logisticsDetails.Status}】。当前位置：{logisticsDetails.CurrentLocation}。";
            }
            else
            {
                responseMessage = $"很抱歉，暂时没有查询到运单号【{trackingId}】的物流信息。";
            }

            var messages = new List<ChatMessageDto>
            {
                new ChatMessageDto { IsFromUser = false, Content = responseMessage, MessageType = "text" }
            };

            return new AgentProcessResult(messages, Success: true);
        }
    }
}
