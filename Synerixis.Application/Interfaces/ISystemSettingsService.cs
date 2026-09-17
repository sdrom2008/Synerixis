namespace Synerixis.Application.Interfaces
{
    /// <summary>平台运营开关 + 全局 LLM Provider（带短缓存）</summary>
    public interface ISystemSettingsService
    {
        Task<SystemOpsSettings> GetOpsAsync(CancellationToken ct = default);

        /// <summary>Admin 写入后立刻失效缓存</summary>
        void Invalidate();

        /// <summary>全局 OpenAI-compatible Provider（Active 时优先于 appsettings）</summary>
        Task<LlmProviderSettings> GetLlmProviderAsync(CancellationToken ct = default);

        void InvalidateLlmProvider();
    }

    public sealed record SystemOpsSettings(
        bool MaintenanceMode,
        string DefaultOutboundMode,
        bool AllowNewRegistration);

    /// <summary>Admin 配置的全局 LLM Provider（密钥明文仅服务端持有；API 返回掩码）</summary>
    public sealed record LlmProviderSettings(
        bool Active,
        string? Name,
        string BaseUrl,
        string? ApiKey,
        string Model)
    {
        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
        public string? ApiKeyHint => MaskHint(ApiKey);

        public static string? MaskHint(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            var k = apiKey.Trim();
            if (k.Length <= 4) return "****";
            return "****" + k[^4..];
        }
    }
}
