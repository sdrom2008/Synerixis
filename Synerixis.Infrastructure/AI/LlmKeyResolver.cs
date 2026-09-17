using Microsoft.Extensions.Configuration;
using Synerixis.Application.Interfaces;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 统一解析 LLM：Admin Provider（Active）&gt; appsettings Llm/Tongyi/DashScope &gt; 环境变量。
    /// 商家级 Key 覆盖见 <see cref="LlmRuntime.UseSellerKey"/>。
    /// </summary>
    public static class LlmKeyResolver
    {
        public const string DefaultDashScopeBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public const string DefaultModel = "qwen-plus";

        public static string? ResolvePlatformKey(IConfiguration config, LlmProviderSettings? admin = null)
        {
            if (admin is { Active: true } && !string.IsNullOrWhiteSpace(admin.ApiKey))
                return admin.ApiKey.Trim();

            return FirstNonEmpty(
                config["Llm:ApiKey"],
                config["Tongyi:Qianwen:ApiKey"],
                config["DashScope:ApiKey"],
                Environment.GetEnvironmentVariable("LLM_API_KEY"),
                Environment.GetEnvironmentVariable("TONGYI_API_KEY"),
                Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY"));
        }

        public static string ResolveModel(IConfiguration config, LlmProviderSettings? admin = null)
        {
            if (admin is { Active: true } && !string.IsNullOrWhiteSpace(admin.Model))
                return admin.Model.Trim();

            return FirstNonEmpty(
                       config["Llm:Model"],
                       config["DashScope:ModelId"],
                       config["Tongyi:Qianwen:Model"])
                   ?? DefaultModel;
        }

        public static string ResolveBaseUrl(IConfiguration config, LlmProviderSettings? admin = null)
        {
            if (admin is { Active: true } && !string.IsNullOrWhiteSpace(admin.BaseUrl))
                return NormalizeBaseUrl(admin.BaseUrl);

            return NormalizeBaseUrl(FirstNonEmpty(
                       config["Llm:BaseUrl"],
                       config["Llm:Endpoint"],
                       config["DashScope:BaseUrl"],
                       Environment.GetEnvironmentVariable("LLM_BASE_URL"))
                   ?? DefaultDashScopeBaseUrl);
        }

        public static string NormalizeBaseUrl(string url)
        {
            var u = url.Trim().TrimEnd('/');
            return u;
        }

        public static bool IsLocalBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return false;
            try
            {
                var uri = new Uri(baseUrl.Contains("://") ? baseUrl : "http://" + baseUrl);
                return uri.IsLoopback
                       || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                       || uri.Host is "127.0.0.1" or "::1";
            }
            catch
            {
                return baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                       || baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string? MaskHint(string? apiKey) => LlmProviderSettings.MaskHint(apiKey);

        public static string? FirstNonEmpty(params string?[] values)
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
