namespace Synerixis.Application.Interfaces
{
    public sealed class CbecTranslateResult
    {
        public required string SourceLang { get; init; }
        public required string TargetLang { get; init; }
        public required string Original { get; init; }
        public required string Translated { get; init; }
        /// <summary>llm | degrade | identity</summary>
        public required string Mode { get; init; }
        public string? Warning { get; init; }
        public bool LlmConfigured { get; init; }
    }

    public sealed class CbecDraftRewriteResult
    {
        public required string TargetLang { get; init; }
        public required string Content { get; init; }
        public string? DetectedBuyerLang { get; init; }
        /// <summary>llm | degrade</summary>
        public required string Mode { get; init; }
        public string? Warning { get; init; }
        public bool LlmConfigured { get; init; }
    }

    /// <summary>CBEC 多语：入站翻译到坐席工作语；草稿改写到买家语。无 Key 明确降级，不假流利翻译。</summary>
    public interface ICbecI18nService
    {
        string DetectLanguage(string? text);

        Task<CbecTranslateResult> TranslateAsync(
            string text,
            string targetLang,
            string? sourceLangHint = null,
            string? sellerApiKey = null,
            CancellationToken ct = default);

        Task<CbecDraftRewriteResult> RewriteDraftAsync(
            string draftContent,
            string targetLang,
            string? buyerMessage = null,
            string? sellerApiKey = null,
            CancellationToken ct = default);
    }
}
