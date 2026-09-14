using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Application.DTOs;
using Synerixis.Application.Helpers;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Agents
{
    /// <summary>
    /// Agent specialized in competitor analysis and market positioning.
    /// </summary>
    public class CompetitorAnalysisAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.CompetitorAnalysis;

        private readonly ILlmClient _llmClient;

        public CompetitorAnalysisAgent(ILlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var prompt = BuildAnalysisPrompt(userInput, context);
            var usage = LlmUsageHelper.FromChatContext(context, AiUsagePurposes.Competitor);
            var response = await _llmClient.GenerateTextAsync(prompt, usage);

            var message = new ChatMessageDto
            {
                IsFromUser = false,
                Content = response,
                MessageType = "text",
                Timestamp = DateTime.UtcNow
            };

            return new AgentProcessResult(
                Messages: new List<ChatMessageDto> { message },
                Success: true
            );
        }

        private string BuildAnalysisPrompt(string userInput, ChatContext context)
        {
            var keyword = context?.ProductName ?? userInput;
            return $@"
你是一个电商市场分析专家，擅长竞品分析和差异化定位。

分析目标：{keyword}

请从以下维度进行分析（如果数据不足可说明需要补充）：
1. 市场概况：需求趋势、价格带分布、销量区间
2. 头部竞品（3-5款）：价格、销量、评分、核心卖点
3. 竞争强度：红海/蓝海，头部集中度
4. 机会点：价格空白、功能缺口、人群细分机会
5. 入局建议：定价策略、主打卖点、推广渠道

输出格式：Markdown 报告，包含表格和要点。";
        }
    }
}
