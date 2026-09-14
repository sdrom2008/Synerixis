using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.Agents
{
    public class GeneralChatAgent : IGeneralChatAgent
    {
        private const string DefaultModel = "qwen-max";
        private readonly IChatCompletionService _chatService;
        private readonly ILogger<GeneralChatAgent> _logger;
        private readonly IAiUsageRecorder? _usageRecorder;
        private readonly IQuickReplyContextProvider? _quickReplies;

        public GeneralChatAgent(
            IChatCompletionService chatService,
            ILogger<GeneralChatAgent> logger,
            IAiUsageRecorder? usageRecorder = null,
            IQuickReplyContextProvider? quickReplies = null)
        {
            _chatService = chatService;
            _logger = logger;
            _usageRecorder = usageRecorder;
            _quickReplies = quickReplies;
        }

        public async Task<string> GenerateReplyAsync(string input, ChatContext context)
        {
            try
            {
                var historyText = string.Join("\n", context.Messages.TakeLast(5)
                    .Select(m => $"{(m.IsFromUser ? "用户" : "AI")}: {m.Content}"));

                var kb = new StringBuilder();
                if (_quickReplies != null && context.ShopId != Guid.Empty)
                {
                    var snippets = await _quickReplies.GetActiveForShopAsync(context.ShopId, 8);
                    if (snippets.Count > 0)
                    {
                        kb.AppendLine("本店快捷回复参考（可酌情改写，勿原样堆砌）：");
                        foreach (var s in snippets)
                            kb.AppendLine($"- [{s.Title}] {s.Content}");
                    }
                }

                var prompt = $"""
你是 Synerixis 的智能小二，友好亲切，用口语回复。
{kb}
历史：{historyText}
用户：{input}
直接回复，不要解释。
""";

                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage("你是友好客服");
                chatHistory.AddUserMessage(prompt);

                var settings = new OpenAIPromptExecutionSettings { Temperature = 0.8, MaxTokens = 150 };

                var response = await _chatService.GetChatMessageContentAsync(chatHistory, settings);

                if (_usageRecorder != null)
                {
                    Guid sellerId = context.ShopId;
                    if (sellerId == Guid.Empty && Guid.TryParse(context.SellerId, out var parsed))
                        sellerId = parsed;
                    if (sellerId != Guid.Empty)
                    {
                        Guid? sessionId = null;
                        if (Guid.TryParse(context.ConversationId, out var sid))
                            sessionId = sid;
                        await _usageRecorder.RecordFromChatResultAsync(
                            sellerId, sessionId, AiUsagePurposes.Draft, DefaultModel, response);
                    }
                }

                return response.Content ?? "抱歉，我没听清～再说一次？";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI 调用失败，使用本地兜底回复");
                return GetLocalResponse(input);
            }
        }

        private string GetLocalResponse(string input)
        {
            var lower = input.ToLowerInvariant();
            if (lower.Contains("你好") || lower.Contains("在吗")) return "您好！我是 Synerixis 智能客服，有什么可以帮您？";
            if (lower.Contains("价格") || lower.Contains("多少钱")) return "我们的服务有免费试用版和付费版，具体价格请咨询销售人员。";
            if (lower.Contains("功能") || lower.Contains("服务")) return "我们提供 AI 营销文案、智能客服、商品优化等服务。";
            if (lower.Contains("再见") || lower.Contains("拜拜")) return "再见！祝您生活愉快～";
            return "抱歉，我现在无法连接 AI 服务，您的问题已记录，稍后会有客服联系您。";
        }
    }
}
