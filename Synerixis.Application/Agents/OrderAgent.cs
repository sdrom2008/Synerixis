using System.Collections.Generic;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Infrastructure;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// Agent responsible for handling order-related queries.
    /// </summary>
    public class OrderAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.QueryOrder;

        private readonly IECommercePlatformClient _platformClient;

        public OrderAgent(IECommercePlatformClient platformClient)
        {
            _platformClient = platformClient;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var orderId = "SIMULATED_ORDER_12345";

            var orderDetails = await _platformClient.GetOrderDetailsAsync(context.Platform, orderId);

            string responseMessage;
            if (orderDetails != null)
            {
                responseMessage = $"您好，查询到订单【{orderId}】的状态是：【{orderDetails.Status}】。订单金额：{orderDetails.Amount}元。";
            }
            else
            {
                responseMessage = $"很抱歉，暂时没有查询到订单【{orderId}】的信息，请您核对一下订单号是否正确。";
            }

            var messages = new List<ChatMessageDto>
            {
                new ChatMessageDto { IsFromUser = false, Content = responseMessage, MessageType = "text" }
            };

            return new AgentProcessResult(messages, Success: true);
        }
    }
}
