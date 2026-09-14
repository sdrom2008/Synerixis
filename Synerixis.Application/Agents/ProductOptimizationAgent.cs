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
    /// Agent specialized in product optimization (title, description, images, marketing).
    /// </summary>
    public class ProductOptimizationAgent : IAgent
    {
        public ChatIntent SupportedIntent => ChatIntent.ProductOptimization;

        private readonly ILlmClient _llmClient;

        public ProductOptimizationAgent(ILlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task<AgentProcessResult> ProcessAsync(string userInput, ChatContext context)
        {
            var prompt = BuildOptimizationPrompt(userInput, context);
            var usage = LlmUsageHelper.FromChatContext(context, AiUsagePurposes.Product);
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

        private string BuildOptimizationPrompt(string userInput, ChatContext context)
        {
            var productInfo = context?.ProductDescription ?? "未提供商品信息";
            var platform = context?.TargetPlatform ?? "通用电商平台";

            return $@"
你是一个顶级电商运营专家，擅长商品优化和营销策划。

用户需求：{userInput}

当前商品信息：
{productInfo}

目标平台：{platform}

请提供以下内容（结构化输出）：
1. 优化后的标题（SEO友好，包含核心关键词）
2. 详情页优化建议（卖点提炼、结构建议）
3. 营销方案（短视频脚本、种草文案、直播话术）
4. 图片优化建议（需要生成的AI绘图提示词）

请用中文回复，尽量具体可执行。";
        }
    }
}
