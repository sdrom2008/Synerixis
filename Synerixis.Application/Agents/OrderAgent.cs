using System.Threading.Tasks;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces.Agents;
using Synerixis.Application.Interfaces.Infrastructure;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// Agent responsible for handling order-related queries.
    /// </summary>
    public class OrderAgent : IAgent
    {
        public string Name => "QueryOrder";
        private readonly IECommercePlatformClient _platformClient;

        public OrderAgent(IECommercePlatformClient platformClient)
        {
            _platformClient = platformClient;
        }

        public async Task<AgentResult> ProcessAsync(Conversation conversation)
        {
            // In a real scenario, the AI (IntentClassifier) would extract parameters like order ID.
            // For now, we'll simulate this.
            var orderId = "SIMULATED_ORDER_12345"; // Placeholder

            // Call the infrastructure layer to get data from the external platform (Taobao/Douyin)
            var orderDetails = await _platformClient.GetOrderDetailsAsync(conversation.Platform, orderId);

            // Format the raw data into a human-readable response
            string responseMessage;
            if (orderDetails != null)
            {
                responseMessage = $"您好，查询到订单【{orderId}】的状态是：【{orderDetails.Status}】。订单金额：{orderDetails.Amount}元。";
            }
            else
            {
                responseMessage = $"很抱歉，暂时没有查询到订单【{orderId}】的信息，请您核对一下订单号是否正确。";
            }

            return new AgentResult(Name, responseMessage, true);
        }
    }

}
