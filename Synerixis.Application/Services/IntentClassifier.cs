using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;

namespace Synerixis.Application.Services;

public class IntentClassifier : IIntentClassifier
{
    private const string DefaultModel = "qwen-max";
    private readonly IChatCompletionService _chatService;
    private readonly IAiUsageRecorder? _usageRecorder;

    public IntentClassifier(IChatCompletionService chatService, IAiUsageRecorder? usageRecorder = null)
    {
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _usageRecorder = usageRecorder;
    }

    public async Task<ChatIntent> ClassifyAsync(string userInput, IReadOnlyList<ChatMessageDto> recentHistory)
    {
        var result = await ClassifyWithConfidenceAsync(userInput, recentHistory);
        return result.Intent;
    }

    public async Task<IntentClassificationResult> ClassifyWithConfidenceAsync(
        string userInput,
        IReadOnlyList<ChatMessageDto> recentHistory,
        Guid? sellerId = null,
        Guid? sessionId = null)
    {
        var historyText = string.Join("\n", recentHistory
            .TakeLast(6)
            .Select(m => $"{(m.IsFromUser ? "用户" : "AI")}: {m.Content.Trim()}"));

        var systemPrompt = """
你是一个精准的意图分类器。
只输出以下其中一个类别英文名称，不要输出任何其他文字、解释、标点或换行。

可用类别（严格只能选其中之一）：
GeneralChat          - 普通闲聊、问候、天气、表情、夸赞、无明确业务需求
OrderQuery           - 查询订单、物流状态、发货时间、到货时间、订单详情
ProductOptimization  - 商品标题/描述/主图/详情页优化、文案建议、图片分析
Appointment          - 预约时间、咨询档期、空位查询、安排见面
AfterSale            - 退款、退货、换货、投诉、售后服务问题
MarketingFollowup    - 复购引导、商品推荐、催评价、感谢、促销活动相关

""";

        var userMessage = $"""
用户最新消息：{userInput}

最近几轮对话：
{historyText}

现在分类，只输出类别名称，例如：GeneralChat
""";

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userMessage);

        try
        {
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                MaxTokens = 20,
                TopP = 0.1
            };

            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: executionSettings
            );

            if (_usageRecorder != null && sellerId.HasValue && sellerId.Value != Guid.Empty)
            {
                await _usageRecorder.RecordFromChatResultAsync(
                    sellerId.Value, sessionId, AiUsagePurposes.Classify, DefaultModel, result,
                    userMessage, result.Content);
            }

            var raw = result.Content?.Trim() ?? "";
            var category = raw.ToLowerInvariant();

            var intent = category switch
            {
                "generalchat" => ChatIntent.GeneralChat,
                "orderquery" => ChatIntent.OrderQuery,
                "productoptimization" => ChatIntent.ProductOptimization,
                "appointment" => ChatIntent.Appointment,
                "aftersale" => ChatIntent.AfterSale,
                "marketingfollowup" => ChatIntent.MarketingFollowup,
                _ => ChatIntent.Unknown
            };

            // 粗置信度：明确命中较高；Unknown 偏低（触发 AutoHandoff）
            double confidence = intent switch
            {
                ChatIntent.Unknown => 0.25,
                ChatIntent.GeneralChat => 0.70,
                ChatIntent.AfterSale => 0.80,
                _ => 0.85
            };

            return new IntentClassificationResult
            {
                Intent = intent,
                Confidence = confidence,
                RawLabel = raw
            };
        }
        catch
        {
            return new IntentClassificationResult
            {
                Intent = ChatIntent.Unknown,
                Confidence = 0.15,
                RawLabel = null
            };
        }
    }
}
