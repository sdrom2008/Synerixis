using System;
using System.Globalization;
using System.Linq;

namespace Synerixis.Application.Helpers
{
    /// <summary>营业时间 / 敏感词 / 自动 handoff 策略工具（Webhook 与配置共用）</summary>
    public static class OutboundPolicyHelper
    {
        public static readonly string DefaultSensitiveKeywords =
            "退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼";

        public static bool TryGetLocalNow(string? timeZoneId, out DateTime localNow, out TimeZoneInfo tz)
        {
            var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Shanghai" : timeZoneId.Trim();
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"); }
                catch
                {
                    // Windows 兼容回退
                    try { tz = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"); }
                    catch
                    {
                        tz = TimeZoneInfo.Utc;
                    }
                }
            }
            catch (InvalidTimeZoneException)
            {
                tz = TimeZoneInfo.Utc;
            }

            localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return true;
        }

        /// <summary>
        /// 判断本地时间是否在营业时段内。[start, end)；跨午夜（如 22:00-09:00）亦支持。
        /// 解析失败时视为「在营业内」（不误伤）。
        /// </summary>
        public static bool IsWithinBusinessHours(string? startHm, string? endHm, string? timeZoneId)
        {
            TryGetLocalNow(timeZoneId, out var localNow, out _);
            if (!TryParseHm(startHm, out var start) || !TryParseHm(endHm, out var end))
                return true;

            var t = localNow.TimeOfDay;
            if (start <= end)
                return t >= start && t < end;
            // 跨午夜
            return t >= start || t < end;
        }

        public static bool TryParseHm(string? hm, out TimeSpan span)
        {
            span = default;
            if (string.IsNullOrWhiteSpace(hm)) return false;
            return TimeSpan.TryParseExact(hm.Trim(), new[] { @"hh\:mm", @"h\:mm" },
                       CultureInfo.InvariantCulture, out span)
                   || TimeSpan.TryParse(hm.Trim(), CultureInfo.InvariantCulture, out span);
        }

        public static string? FindSensitiveHit(string? content, string? keywordsCsv)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            var csv = string.IsNullOrWhiteSpace(keywordsCsv) ? DefaultSensitiveKeywords : keywordsCsv;
            var keywords = csv.Split(new[] { ',', '，', ';', '；', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToList();
            foreach (var kw in keywords)
            {
                if (content.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return kw;
            }
            return null;
        }
    }
}
