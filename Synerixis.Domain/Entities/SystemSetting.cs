using System;
using System.ComponentModel.DataAnnotations;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 平台级运营开关与 LLM Provider（key-value）。
    /// 运营安全项见 <see cref="SystemSettingKeys"/>；LLM 密钥仅 Admin 读写且 GET 时掩码。
    /// </summary>
    public class SystemSetting
    {
        [MaxLength(64)]
        public string Key { get; set; } = string.Empty;

        /// <summary>最长 2048，以容纳 LLM API Key / BaseUrl。</summary>
        [MaxLength(2048)]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Admin 可写的安全运营设置键（不含密钥）</summary>
    public static class SystemSettingKeys
    {
        public const string MaintenanceMode = "MaintenanceMode";
        public const string DefaultOutboundMode = "DefaultOutboundMode";
        public const string AllowNewRegistration = "AllowNewRegistration";

        public static readonly string[] WritableKeys =
        {
            MaintenanceMode,
            DefaultOutboundMode,
            AllowNewRegistration
        };

        public static bool IsWritable(string key) =>
            Array.Exists(WritableKeys, k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>全局 OpenAI-compatible LLM Provider（Admin 配置；Active 时优先于 appsettings）</summary>
    public static class LlmProviderSettingKeys
    {
        public const string Active = "LlmProvider.Active";
        public const string Name = "LlmProvider.Name";
        public const string BaseUrl = "LlmProvider.BaseUrl";
        public const string ApiKey = "LlmProvider.ApiKey";
        public const string Model = "LlmProvider.Model";

        public static readonly string[] AllKeys =
        {
            Active,
            Name,
            BaseUrl,
            ApiKey,
            Model
        };
    }
}
