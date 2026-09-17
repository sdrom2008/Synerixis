using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Synerixis.Application.Interfaces;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 进程内 LLM 运行时：Admin Provider / 配置 Key + 可选商家级覆盖（AsyncLocal）。
    /// OpenAI-compatible only（cloud + Ollama / LM Studio）。
    /// 无 Key（且非本地 endpoint）时不抛启动异常；调用方应先查 <see cref="IsConfigured"/> 并降级。
    /// </summary>
    public sealed class LlmRuntime
    {
        private static readonly AsyncLocal<string?> ScopedSellerKey = new();
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LlmRuntime> _logger;
        private readonly ConcurrentDictionary<string, Kernel> _kernels = new(StringComparer.Ordinal);
        private int _warnedMissing;

        public LlmRuntime(
            IConfiguration config,
            IServiceScopeFactory scopeFactory,
            ILogger<LlmRuntime> logger)
        {
            _config = config;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>Admin Active Provider（短缓存）；未 Active 时字段可能仍有草稿值。</summary>
        public LlmProviderSettings GetAdminProvider()
        {
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            return svc.GetLlmProviderAsync().GetAwaiter().GetResult();
        }

        public string? PlatformKey
        {
            get
            {
                var admin = GetAdminProvider();
                return LlmKeyResolver.ResolvePlatformKey(_config, admin);
            }
        }

        public string ModelId
        {
            get
            {
                var admin = GetAdminProvider();
                return LlmKeyResolver.ResolveModel(_config, admin);
            }
        }

        public string BaseUrl
        {
            get
            {
                var admin = GetAdminProvider();
                return LlmKeyResolver.ResolveBaseUrl(_config, admin);
            }
        }

        /// <summary>当前有效 Key = 商家覆盖 ?? 平台（Admin Active / appsettings）。</summary>
        public string? EffectiveKey =>
            LlmKeyResolver.FirstNonEmpty(ScopedSellerKey.Value, PlatformKey);

        /// <summary>
        /// 有商家/平台 Key，或本地 OpenAI-compatible（Ollama/LM Studio）已配 BaseUrl+Model。
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(EffectiveKey))
                    return true;

                // 本地 OpenAI-compatible（Ollama / LM Studio）允许无 Key
                var baseUrl = BaseUrl;
                var model = ModelId;
                return LlmKeyResolver.IsLocalBaseUrl(baseUrl)
                       && !string.IsNullOrWhiteSpace(model);
            }
        }

        public bool PlatformConfigured =>
            !string.IsNullOrWhiteSpace(PlatformKey)
            || (LlmKeyResolver.IsLocalBaseUrl(BaseUrl) && !string.IsNullOrWhiteSpace(ModelId));

        public string? KeyHint => LlmKeyResolver.MaskHint(EffectiveKey);

        public string Source
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ScopedSellerKey.Value)) return "seller";
                var admin = GetAdminProvider();
                if (admin.Active) return "admin";
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

        /// <summary>Admin 保存/激活后清空 Kernel 缓存。</summary>
        public void InvalidateKernels()
        {
            _kernels.Clear();
            Interlocked.Exchange(ref _warnedMissing, 0);
            _logger.LogInformation("[Llm] Kernel cache cleared after provider change");
        }

        public IChatCompletionService GetChatService()
        {
            if (!IsConfigured)
            {
                WarnMissingOnce();
                throw new InvalidOperationException(
                    "未配置 AI：请在 Admin「LLM Provider」或商家「AI 设置」配置，或设 Llm:ApiKey / LLM_API_KEY；本地可配 Ollama/LM Studio BaseUrl。");
            }

            var key = EffectiveKey;
            var apiKey = string.IsNullOrWhiteSpace(key) ? "local" : key;
            var modelId = ModelId;
            var baseUrl = BaseUrl;
            var cacheKey = $"{baseUrl}|{modelId}|{apiKey}";
            var kernel = _kernels.GetOrAdd(cacheKey, _ => BuildKernel(apiKey, modelId, baseUrl));
            return kernel.GetRequiredService<IChatCompletionService>();
        }

        /// <summary>
        /// DI 用：无配置时返回占位 Chat（调用会失败）；业务路径应先查 <see cref="IsConfigured"/>。
        /// </summary>
        public IChatCompletionService GetChatServiceOrPlaceholder()
        {
            if (IsConfigured)
                return GetChatService();
            WarnMissingOnce();
            var modelId = ModelId;
            var baseUrl = BaseUrl;
            var kernel = _kernels.GetOrAdd("__not_configured__",
                _ => BuildKernel("sk-not-configured", modelId, baseUrl));
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
            if (!IsConfigured)
                throw new InvalidOperationException("未配置 AI");
            var key = EffectiveKey;
            var apiKey = string.IsNullOrWhiteSpace(key) ? "local" : key;
            var modelId = ModelId;
            var baseUrl = BaseUrl;
            var cacheKey = $"{baseUrl}|{modelId}|{apiKey}";
            return _kernels.GetOrAdd(cacheKey, _ => BuildKernel(apiKey, modelId, baseUrl));
        }

        private void WarnMissingOnce()
        {
            if (Interlocked.Exchange(ref _warnedMissing, 1) == 0)
            {
                _logger.LogWarning(
                    "[Llm] 未配置可用 Provider：起草将走规则降级。Admin「LLM Provider」、merchant-web「AI 设置」，或 Llm:ApiKey / LLM_API_KEY / 本地 BaseUrl。");
            }
        }

        private static Kernel BuildKernel(string apiKey, string modelId, string baseUrl)
        {
            var builder = Kernel.CreateBuilder();
            var endpoint = new Uri(string.IsNullOrWhiteSpace(baseUrl)
                ? LlmKeyResolver.DefaultDashScopeBaseUrl
                : baseUrl);
            builder.AddOpenAIChatCompletion(
                modelId: string.IsNullOrWhiteSpace(modelId) ? LlmKeyResolver.DefaultModel : modelId,
                apiKey: string.IsNullOrWhiteSpace(apiKey) ? "local" : apiKey,
                endpoint: endpoint);
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
