using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Infrastructure;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// 订单查询 Agent
    /// </summary>
    public class OrderAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.QueryOrder;

        private readonly IECommercePlatformClient _platformClient;
        private readonly IChatSessionRepository _chatSessionRepository;

        public OrderAgent(
            IECommercePlatformClient platformClient,
            IChatSessionRepository chatSessionRepository)
        {
            _platformClient = platformClient;
            _chatSessionRepository = chatSessionRepository;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            // 1. 从 ChatSession 获取客户信息
            var chatSession = await _chatSessionRepository.GetByCustomerIdAsync(context.CustomerId);

            if (chatSession == null)
            {
                return new AgentProcessResult(
                    Messages: Array.Empty<ChatMessageDto>(),
                    Success: false,
                    ErrorMessage: "未找到客户会话信息，无法查询订单");
            }

            // 2. 获取订单信息
            var orderResult = await _platformClient.GetOrderDetailsAsync(
                platform: chatSession.Platform,
                orderId: chatSession.CustomerId);

            if (orderResult != null)
            {
                var response = $"您好，查询到您的订单【{orderResult.OrderId}】，状态：{orderResult.Status}，金额：{orderResult.Amount}元。";
                var messages = new List<ChatMessageDto>
                {
                    new ChatMessageDto 
                    { 
                        IsFromUser = false, 
                        Content = response, 
                        MessageType = "text",
                        Data = new { orderResult.OrderId, orderResult.Status, orderResult.Amount }
                    }
                };
                return new AgentProcessResult(messages, Success: true);
            }

            return new AgentProcessResult(
                Messages: Array.Empty<ChatMessageDto>(),
                Success: false,
                ErrorMessage: "未找到订单信息");
        }
    }
}
