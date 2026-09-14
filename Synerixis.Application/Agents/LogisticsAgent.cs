using System;
using System.Collections.Generic;
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
    /// Agent responsible for handling logistics-related queries.
    /// 优先用结构化物流数据；有 ILlmClient 时润色话术并记账 purpose=logistics。
    /// </summary>
    public class LogisticsAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.LogisticsQuery;

        private readonly IECommercePlatformClient _platformClient;
        private readonly ILlmClient? _llmClient;

        public LogisticsAgent(
            IECommercePlatformClient platformClient,
            ILlmClient? llmClient = null)
        {
            _platformClient = platformClient;
            _llmClient = llmClient;
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

            if (_llmClient != null)
            {
                try
                {
                    var prompt = $"""
你是跨境电商客服，根据以下物流事实用口语简短回复买家（中文，不超过 80 字，不要编造）：
用户问：{userInput}
事实：{responseMessage}
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
                new ChatMessageDto { IsFromUser = false, Content = responseMessage, MessageType = "text", Timestamp = DateTime.UtcNow }
            };

            return new AgentProcessResult(messages, Success: true);
        }
    }
}
