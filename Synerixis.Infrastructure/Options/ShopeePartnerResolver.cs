using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Synerixis.Infrastructure.Options
{
    /// <summary>
    /// 单站点 partner 凭据（Host + PartnerId/Key）。
    /// </summary>
    public sealed class ShopeePartnerConfig
    {
        public string Region { get; init; } = "SG";
        public string Host { get; init; } = "https://partner.shopeemobile.com";
        public string? PartnerId { get; init; }
        public string? PartnerKey { get; init; }
        public string? RedirectUri { get; init; }

        public string Endpoint => (Host ?? string.Empty).TrimEnd('/');
    }

    /// <summary>
    /// 解析 Shopee 多区域 partner 配置。
    /// 兼容现有单组 <c>Shopee:AppKey</c>/<c>AppSecret</c>/<c>Endpoint</c>；
    /// 可选 <c>Shopee:Partners</c> 数组或 <c>Shopee:{Region}:Host/PartnerId/PartnerKey</c>。
    /// </summary>
    public static class ShopeePartnerResolver
    {
        public const string DefaultRegion = "SG";
        public const string DefaultHost = "https://partner.shopeemobile.com";

        /// <summary>常用站点（绑店下拉 + 默认 Host）。</summary>
        public static readonly IReadOnlyList<(string Code, string Label)> CommonRegions = new[]
        {
            ("SG", "新加坡 / 全球"),
            ("TW", "台湾"),
            ("VN", "越南"),
            ("PH", "菲律宾"),
            ("MY", "马来西亚"),
            ("TH", "泰国"),
            ("ID", "印尼"),
            ("BR", "巴西"),
            ("MX", "墨西哥"),
            ("CO", "哥伦比亚"),
            ("CL", "智利"),
            ("CN", "中国大陆"),
        };

        private static readonly HashSet<string> KnownRegions = new(StringComparer.OrdinalIgnoreCase)
        {
            "SG", "TW", "VN", "PH", "MY", "TH", "ID", "BR", "MX", "CO", "CL", "CN"
        };

        private static readonly Dictionary<string, string> DefaultHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SG"] = "https://partner.shopeemobile.com",
            ["TW"] = "https://partner.tw.shopeemobile.com",
            ["VN"] = "https://partner.vn.shopeemobile.com",
            ["PH"] = "https://partner.ph.shopeemobile.com",
            ["MY"] = "https://partner.my.shopeemobile.com",
            ["TH"] = "https://partner.th.shopeemobile.com",
            ["ID"] = "https://partner.id.shopeemobile.com",
            ["BR"] = "https://partner.br.shopeemobile.com",
            ["MX"] = "https://partner.mx.shopeemobile.com",
            ["CO"] = "https://partner.co.shopeemobile.com",
            ["CL"] = "https://partner.cl.shopeemobile.com",
            ["CN"] = "https://openplatform.shopee.cn",
        };

        public static string NormalizeRegion(string? region)
        {
            var r = (region ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrEmpty(r) ? DefaultRegion : r;
        }

        public static bool IsKnownRegion(string? region)
            => !string.IsNullOrWhiteSpace(region) && KnownRegions.Contains(region.Trim());

        /// <summary>
        /// 配置里的默认区域（<c>Shopee:Region</c>），缺省 SG。
        /// </summary>
        public static string DefaultConfiguredRegion(IConfiguration config)
            => NormalizeRegion(config["Shopee:Region"]);

        public static ShopeePartnerConfig Resolve(IConfiguration config, string? region)
        {
            var global = ReadGlobal(config);
            var code = NormalizeRegion(string.IsNullOrWhiteSpace(region) ? global.Region : region);

            ShopeePartnerConfig? overlay = FindInPartnersArray(config, code)
                ?? ReadRegionSection(config, code);

            var host = FirstNonEmpty(overlay?.Host, global.Host, LookupDefaultHost(code, global.Host));
            var partnerId = FirstNonEmpty(overlay?.PartnerId, global.PartnerId);
            var partnerKey = FirstNonEmpty(overlay?.PartnerKey, global.PartnerKey);
            var redirect = FirstNonEmpty(overlay?.RedirectUri, global.RedirectUri);

            return new ShopeePartnerConfig
            {
                Region = code,
                Host = string.IsNullOrWhiteSpace(host) ? DefaultHost : host.TrimEnd('/'),
                PartnerId = partnerId,
                PartnerKey = partnerKey,
                RedirectUri = redirect
            };
        }

        /// <summary>Webhook 验签：默认 secret + 各 Partners 的 PartnerKey（去重）。</summary>
        public static IEnumerable<string> AllPartnerKeys(IConfiguration config)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var global = ReadGlobal(config);
            if (!string.IsNullOrEmpty(global.PartnerKey) && seen.Add(global.PartnerKey))
                yield return global.PartnerKey;

            foreach (var p in ReadPartnersArray(config))
            {
                if (!string.IsNullOrEmpty(p.PartnerKey) && seen.Add(p.PartnerKey))
                    yield return p.PartnerKey;
            }

            foreach (var (code, _) in CommonRegions)
            {
                var section = config.GetSection($"Shopee:{code}");
                var key = FirstNonEmpty(section["PartnerKey"], section["AppSecret"]);
                if (!string.IsNullOrEmpty(key) && seen.Add(key))
                    yield return key;
            }
        }

        private static ShopeePartnerConfig ReadGlobal(IConfiguration config)
        {
            var region = NormalizeRegion(config["Shopee:Region"]);
            var host = FirstNonEmpty(
                config["Shopee:Endpoint"],
                config["Shopee:ApiBaseUrl"],
                config["Shopee:Host"],
                LookupDefaultHost(region, DefaultHost));
            return new ShopeePartnerConfig
            {
                Region = region,
                Host = (host ?? DefaultHost).TrimEnd('/'),
                PartnerId = FirstNonEmpty(config["Shopee:AppKey"], config["Shopee:PartnerId"]),
                PartnerKey = FirstNonEmpty(config["Shopee:AppSecret"], config["Shopee:PartnerKey"]),
                RedirectUri = FirstNonEmpty(config["Shopee:RedirectUri"], config["Shopee:RedirectUrl"])
            };
        }

        private static ShopeePartnerConfig? FindInPartnersArray(IConfiguration config, string region)
        {
            foreach (var p in ReadPartnersArray(config))
            {
                if (string.Equals(NormalizeRegion(p.Region), region, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private static List<ShopeePartnerConfig> ReadPartnersArray(IConfiguration config)
        {
            var list = new List<ShopeePartnerConfig>();
            var section = config.GetSection("Shopee:Partners");
            foreach (var child in section.GetChildren())
            {
                var region = NormalizeRegion(child["Region"] ?? child["Code"]);
                var host = FirstNonEmpty(child["Host"], child["Endpoint"], child["ApiBaseUrl"]);
                var partnerId = FirstNonEmpty(child["PartnerId"], child["AppKey"]);
                var partnerKey = FirstNonEmpty(child["PartnerKey"], child["AppSecret"]);
                var redirect = FirstNonEmpty(child["RedirectUri"], child["RedirectUrl"]);
                list.Add(new ShopeePartnerConfig
                {
                    Region = region,
                    Host = host ?? string.Empty,
                    PartnerId = partnerId,
                    PartnerKey = partnerKey,
                    RedirectUri = redirect
                });
            }
            return list;
        }

        private static ShopeePartnerConfig? ReadRegionSection(IConfiguration config, string region)
        {
            if (!IsKnownRegion(region)) return null;
            var section = config.GetSection($"Shopee:{region}");
            if (!section.GetChildren().Any()) return null;

            return new ShopeePartnerConfig
            {
                Region = region,
                Host = FirstNonEmpty(section["Host"], section["Endpoint"], section["ApiBaseUrl"]) ?? string.Empty,
                PartnerId = FirstNonEmpty(section["PartnerId"], section["AppKey"]),
                PartnerKey = FirstNonEmpty(section["PartnerKey"], section["AppSecret"]),
                RedirectUri = FirstNonEmpty(section["RedirectUri"], section["RedirectUrl"])
            };
        }

        private static string LookupDefaultHost(string region, string? fallback)
        {
            if (DefaultHosts.TryGetValue(region, out var host))
                return host;
            return string.IsNullOrWhiteSpace(fallback) ? DefaultHost : fallback;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return null;
        }
    }
}
