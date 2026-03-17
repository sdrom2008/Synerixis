using System.Threading.Tasks;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces.Agents;
using Synerixis.Application.Interfaces.Infrastructure;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// Agent responsible for handling logistics-related queries.
    /// </summary>
    public class LogisticsAgent : IAgent
    {
        public string Name => "QueryLogistics";
        private readonly IECommercePlatformClient _platformClient;

        public LogisticsAgent(IECommercePlatformClient platformClient)
        {
            _platformClient = platformClient;
        }

        public async Task<AgentResult> ProcessAsync(Conversation conversation)
        {
            var trackingId = "SIMULATED_TRACKING_67890"; // Placeholder

            var logisticsDetails = await _platformClient.GetLogisticsDetailsAsync(conversation.Platform, trackingId);

            string responseMessage;
            if (logisticsDetails != null)
            {
                responseMessage = $"您好，查询到运单号【{trackingId}】的最新状态是：【{logisticsDetails.Status}】。当前位置：{logisticsDetails.CurrentLocation}。";
            }
            else
            {
                responseMessage = $"很抱歉，暂时没有查询到运单号【{trackingId}】的物流信息。";
            }

            return new AgentResult(Name, responseMessage, true);
        }
    }

}
