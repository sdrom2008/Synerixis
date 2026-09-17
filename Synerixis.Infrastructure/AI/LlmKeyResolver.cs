using Microsoft.Extensions.Configuration;

namespace Synerixis.Infrastructure.AI
{
    /// <summary>
    /// 统一解析 LLM API Key：appsettings Llm/Tongyi/DashScope + 环境变量。
    /// 商家级覆盖见 <see cref="LlmRuntime.UseSellerKey"/>.
    /// </summary>
    public static class LlmKeyResolver
    {
        public static string? ResolvePlatformKey(IConfiguration config)
        {
            return FirstNonEmpty(
                config["Llm:ApiKey"],
                config["Tongyi:Qianwen:ApiKey"],
                config["DashScope:ApiKey"],
                Environment.GetEnvironmentVariable("LLM_API_KEY"),
                Environment.GetEnvironmentVariable("TONGYI_API_KEY"),
                Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY"));
        }

        public static string ResolveModel(IConfiguration config)
        {
            return FirstNonEmpty(
                       config["Llm:Model"],
                       config["DashScope:ModelId"],
                       config["Tongyi:Qianwen:Model"])
                   ?? "qwen-plus";
        }

        public static string? MaskHint(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            var k = apiKey.Trim();
            if (k.Length <= 4) return "****";
            return "****" + k[^4..];
        }

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
