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
        // 规则优先：关键词命中直接返回，高置信，与 AgentRouter 对齐
        var ruleHit = TryClassifyByRules(userInput);
        if (ruleHit != null)
            return ruleHit;

        var historyText = string.Join("\n", recentHistory
            .TakeLast(6)
            .Select(m => $"{(m.IsFromUser ? "用户" : "AI")}: {m.Content.Trim()}"));

        var systemPrompt = """
你是一个精准的意图分类器。
只输出以下其中一个类别英文名称，不要输出任何其他文字、解释、标点或换行。

可用类别（严格只能选其中之一）：
GeneralChat          - 普通闲聊、问候、天气、表情、夸赞、无明确业务需求
OrderQuery           - 查询订单、发货时间、到货时间、订单详情、查单号、退款进度
LogisticsQuery       - 运单号、快递、物流轨迹、tracking、shipment、包裹在哪
CompetitorAnalysis   - 竞品、对手、对比价格、竞品分析、市场对比
ProductOptimization  - 商品标题/描述/主图/详情页优化、文案建议、图片分析
Appointment          - 预约时间、咨询档期、空位查询、安排见面
AfterSale            - 退款纠纷、退货、换货、投诉、售后服务问题（不含单纯查退款进度）
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
                "logisticsquery" => ChatIntent.LogisticsQuery,
                "competitoranalysis" => ChatIntent.CompetitorAnalysis,
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
            // LLM 失败时不要用超低置信误触发 AutoHandoff；走 GeneralChat 规则草稿路径
            return new IntentClassificationResult
            {
                Intent = ChatIntent.GeneralChat,
                Confidence = 0.72,
                RawLabel = "GeneralChat(llm-failed)"
            };
        }
    }

    /// <summary>
    /// 关键词规则优先于 LLM。优先级：物流 → 竞品 → 订单。
    /// </summary>
    internal static IntentClassificationResult? TryClassifyByRules(string? userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return null;

        var text = userInput.Trim();
        var lower = text.ToLowerInvariant();

        // LogisticsQuery：运单/快递/物流/tracking/shipment
        if (ContainsAny(text, "运单", "快递", "物流") ||
            ContainsAny(lower, "tracking", "shipment"))
        {
            return new IntentClassificationResult
            {
                Intent = ChatIntent.LogisticsQuery,
                Confidence = 0.95,
                RawLabel = "LogisticsQuery(rule)"
            };
        }

        // CompetitorAnalysis：竞品/对手/对比价格/竞品分析
        if (ContainsAny(text, "竞品", "对手", "对比价格", "竞品分析") ||
            ContainsAny(lower, "competitor"))
        {
            return new IntentClassificationResult
            {
                Intent = ChatIntent.CompetitorAnalysis,
                Confidence = 0.95,
                RawLabel = "CompetitorAnalysis(rule)"
            };
        }

        // OrderQuery：订单/退款进度/查单号
        if (ContainsAny(text, "订单", "退款进度", "查单号") ||
            ContainsAny(lower, "order"))
        {
            return new IntentClassificationResult
            {
                Intent = ChatIntent.OrderQuery,
                Confidence = 0.92,
                RawLabel = "OrderQuery(rule)"
            };
        }

        return null;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (!string.IsNullOrEmpty(n) && haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
