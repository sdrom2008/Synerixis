using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 进程内 LLM 运行时：平台 Key（配置/环境）+ 可选商家级覆盖（AsyncLocal）。
    /// 无 Key 时不抛启动异常；调用方应先查 <see cref="IsConfigured"/> 并降级。
    /// </summary>
    public sealed class LlmRuntime
    {
        private static readonly AsyncLocal<string?> ScopedSellerKey = new();
        private readonly IConfiguration _config;
        private readonly ILogger<LlmRuntime> _logger;
        private readonly ConcurrentDictionary<string, Kernel> _kernels = new(StringComparer.Ordinal);
        private int _warnedMissing;

        public LlmRuntime(IConfiguration config, ILogger<LlmRuntime> logger)
        {
            _config = config;
            _logger = logger;
        }

        public string? PlatformKey => LlmKeyResolver.ResolvePlatformKey(_config);

        public string ModelId => LlmKeyResolver.ResolveModel(_config);

        /// <summary>当前有效 Key = 商家覆盖 ?? 平台配置。</summary>
        public string? EffectiveKey =>
            LlmKeyResolver.FirstNonEmpty(ScopedSellerKey.Value, PlatformKey);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(EffectiveKey);

        public bool PlatformConfigured => !string.IsNullOrWhiteSpace(PlatformKey);

        public string? KeyHint => LlmKeyResolver.MaskHint(EffectiveKey);

        public string Source
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ScopedSellerKey.Value)) return "seller";
                if (PlatformConfigured) return "platform";
                return "none";
            }
        }

        /// <summary>在入站草稿等路径内临时使用商家 LlmApiKey。</summary>
        public IDisposable UseSellerKey(string? sellerApiKey)
        {
            var prev = ScopedSellerKey.Value;
            ScopedSellerKey.Value = string.IsNullOrWhiteSpace(sellerApiKey) ? null : sellerApiKey.Trim();
            return new ScopeRevert(() => ScopedSellerKey.Value = prev);
        }

        public IChatCompletionService GetChatService()
        {
            var key = EffectiveKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingOnce();
                throw new InvalidOperationException("未配置 AI：请在商家「AI 设置」填写 LLM API Key，或配置 Llm:ApiKey / 环境变量 LLM_API_KEY。");
            }

            var kernel = _kernels.GetOrAdd(key, k => BuildKernel(k, ModelId));
            return kernel.GetRequiredService<IChatCompletionService>();
        }

        /// <summary>
        /// DI 用：无 Key 时返回占位 Chat（调用会失败）；业务路径应先查 <see cref="IsConfigured"/>。
        /// </summary>
        public IChatCompletionService GetChatServiceOrPlaceholder()
        {
            if (IsConfigured)
                return GetChatService();
            WarnMissingOnce();
            var kernel = _kernels.GetOrAdd("__not_configured__",
                _ => BuildKernel("sk-not-configured", ModelId));
            return kernel.GetRequiredService<IChatCompletionService>();
        }

        public IChatCompletionService? TryGetChatService()
        {
            try
            {
                if (!IsConfigured) return null;
                return GetChatService();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Llm] TryGetChatService failed");
                return null;
            }
        }

        public Kernel GetKernel()
        {
            var key = EffectiveKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("未配置 AI");
            return _kernels.GetOrAdd(key, k => BuildKernel(k, ModelId));
        }

        private void WarnMissingOnce()
        {
            if (Interlocked.Exchange(ref _warnedMissing, 1) == 0)
            {
                _logger.LogWarning(
                    "[Llm] 未配置 API Key：起草将走规则降级。可在 merchant-web「AI 设置」填写，或设 Llm:ApiKey / LLM_API_KEY。");
            }
        }

        private static Kernel BuildKernel(string apiKey, string modelId)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: string.IsNullOrWhiteSpace(modelId) ? "qwen-plus" : modelId,
                apiKey: apiKey,
                endpoint: new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1"));
            return builder.Build();
        }

        private sealed class ScopeRevert : IDisposable
        {
            private readonly Action _onDispose;
            private int _done;
            public ScopeRevert(Action onDispose) => _onDispose = onDispose;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _done, 1) == 0)
                    _onDispose();
            }
        }
    }
}
