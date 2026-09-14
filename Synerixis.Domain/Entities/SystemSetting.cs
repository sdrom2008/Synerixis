using System;
using System.ComponentModel.DataAnnotations;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 平台级运营开关（key-value）。可覆盖 appsettings 中的安全可写项；禁止存密钥。
    /// </summary>
    public class SystemSetting
    {
        [MaxLength(64)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(512)]
        public string Value { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Admin 可写的安全设置键（勿加入密钥类）</summary>
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
}
