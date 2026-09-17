using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Synerixis.Application.Helpers;
using Synerixis.Application.Interfaces;
using Synerixis.Infrastructure.AI;

namespace Synerixis.Infrastructure.Services
{
    public sealed class CbecI18nService : ICbecI18nService
    {
        private readonly LlmRuntime _llm;
        private readonly ILogger<CbecI18nService> _logger;

        public CbecI18nService(LlmRuntime llm, ILogger<CbecI18nService> logger)
        {
            _llm = llm;
            _logger = logger;
        }

        public string DetectLanguage(string? text) => CbecLanguageHelper.Detect(text);

        public async Task<CbecTranslateResult> TranslateAsync(
            string text,
            string targetLang,
            string? sourceLangHint = null,
            string? sellerApiKey = null,
            CancellationToken ct = default)
        {
            var original = text ?? string.Empty;
            var target = CbecLanguageHelper.Normalize(targetLang);
            var source = string.IsNullOrWhiteSpace(sourceLangHint)
                ? CbecLanguageHelper.Detect(original)
                : CbecLanguageHelper.Normalize(sourceLangHint);

            if (string.IsNullOrWhiteSpace(original))
            {
                return new CbecTranslateResult
                {
                    SourceLang = source,
                    TargetLang = target,
                    Original = original,
                    Translated = original,
                    Mode = "identity",
                    LlmConfigured = false,
                    Warning = "空内容"
                };
            }

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                return new CbecTranslateResult
                {
                    SourceLang = source,
                    TargetLang = target,
                    Original = original,
                    Translated = original,
                    Mode = "identity",
                    LlmConfigured = false,
                    Warning = null
                };
            }

            using (_llm.UseSellerKey(sellerApiKey))
            {
                if (!_llm.IsConfigured)
                {
                    return DegradeTranslate(original, source, target);
                }

                try
                {
                    var chat = _llm.GetChatService();
                    var history = new ChatHistory();
                    history.AddSystemMessage(
                        "You are a precise CBEC customer-service translator. " +
                        "Translate the user message only. Output the translation text alone — no quotes, no commentary.");
                    history.AddUserMessage(
                        $"Source language: {CbecLanguageHelper.PromptLanguageName(source)}\n" +
                        $"Target language: {CbecLanguageHelper.PromptLanguageName(target)}\n\n" +
                        original);
                    var settings = new PromptExecutionSettings
                    {
                        ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.2 }
                    };
                    var result = await chat.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
                    var translated = (result.Content ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(translated))
                        return DegradeTranslate(original, source, target, "模型返回空译文");

                    return new CbecTranslateResult
                    {
                        SourceLang = source,
                        TargetLang = target,
                        Original = original,
                        Translated = translated,
                        Mode = "llm",
                        LlmConfigured = true
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[I18n] Translate failed; degrade");
                    return DegradeTranslate(original, source, target, "翻译调用失败，已降级");
                }
            }
        }

        public async Task<CbecDraftRewriteResult> RewriteDraftAsync(
            string draftContent,
            string targetLang,
            string? buyerMessage = null,
            string? sellerApiKey = null,
            CancellationToken ct = default)
        {
            var content = draftContent ?? string.Empty;
            var target = CbecLanguageHelper.Normalize(targetLang);
            var buyerLang = string.IsNullOrWhiteSpace(buyerMessage)
                ? (string?)null
                : CbecLanguageHelper.Detect(buyerMessage);

            using (_llm.UseSellerKey(sellerApiKey))
            {
                if (!_llm.IsConfigured)
                {
                    return new CbecDraftRewriteResult
                    {
                        TargetLang = target,
                        Content = content,
                        DetectedBuyerLang = buyerLang,
                        Mode = "degrade",
                        LlmConfigured = false,
                        Warning =
                            $"【未配置 AI·无法流利改写为 {CbecLanguageHelper.DisplayName(target)}】" +
                            "请配置 Admin LLM Provider 或本店 Key 后再点「按目标语重写」。草稿原文未改动，勿当作已翻译发出。"
                    };
                }

                try
                {
                    var chat = _llm.GetChatService();
                    var history = new ChatHistory();
                    history.AddSystemMessage(
                        "You rewrite cross-border e-commerce customer-service reply drafts. " +
                        "Keep meaning, tone (professional/friendly), and facts. " +
                        "Output only the rewritten reply in the target language — no preface.");
                    var buyerHint = string.IsNullOrWhiteSpace(buyerMessage)
                        ? ""
                        : $"\nBuyer message (for context):\n{buyerMessage.Trim()}\n";
                    history.AddUserMessage(
                        $"Rewrite the draft into {CbecLanguageHelper.PromptLanguageName(target)}." +
                        buyerHint +
                        $"\nDraft:\n{content}");
                    var settings = new PromptExecutionSettings
                    {
                        ExtensionData = new Dictionary<string, object> { ["temperature"] = 0.4 }
                    };
                    var result = await chat.GetChatMessageContentAsync(history, settings, cancellationToken: ct);
                    var rewritten = (result.Content ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(rewritten))
                    {
                        return new CbecDraftRewriteResult
                        {
                            TargetLang = target,
                            Content = content,
                            DetectedBuyerLang = buyerLang,
                            Mode = "degrade",
                            LlmConfigured = true,
                            Warning = "模型返回空内容，草稿未改动"
                        };
                    }

                    return new CbecDraftRewriteResult
                    {
                        TargetLang = target,
                        Content = rewritten,
                        DetectedBuyerLang = buyerLang,
                        Mode = "llm",
                        LlmConfigured = true
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[I18n] Draft rewrite failed; degrade");
                    return new CbecDraftRewriteResult
                    {
                        TargetLang = target,
                        Content = content,
                        DetectedBuyerLang = buyerLang,
                        Mode = "degrade",
                        LlmConfigured = true,
                        Warning = "改写调用失败，草稿未改动：" + ex.Message
                    };
                }
            }
        }

        private static CbecTranslateResult DegradeTranslate(
            string original, string source, string target, string? extra = null)
        {
            var warn =
                $"【未配置 AI·无法流利翻译】检测语种 {CbecLanguageHelper.DisplayName(source)} → " +
                $"{CbecLanguageHelper.DisplayName(target)}。原文保留；请配置 LLM 后一键翻译，" +
                "或由懂该语言的坐席处理。不做假流利译文。";
            if (!string.IsNullOrWhiteSpace(extra))
                warn = extra + " " + warn;

            return new CbecTranslateResult
            {
                SourceLang = source,
                TargetLang = target,
                Original = original,
                Translated = original,
                Mode = "degrade",
                LlmConfigured = false,
                Warning = warn
            };
        }
    }
}
