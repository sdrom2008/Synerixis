using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Synerixis.Application.DTOs;
using System.Net.Http.Json;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/agent")]
    [Authorize]  // 需要登录（JWT）
    public class AgentController : BaseApiController
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public AgentController(IConfiguration config, IHttpClientFactory factory)
        {
            _config = config;
            _httpClient = factory.CreateClient();
        }

        [HttpPost("optimizeproduct")]
        public async Task<IActionResult> OptimizeProduct([FromBody] OptimizeProductRequest request)
        {
            if (string.IsNullOrEmpty(request.Intent))
                return BadRequest("意图不能为空");

            // 获取通义千问 API Key（从配置或环境变量）
            var apiKey = _config["Tongyi:Qianwen:ApiKey"] ?? Environment.GetEnvironmentVariable("TONGYI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
                return StatusCode(500, "未配置通义千问 API Key");

            // 构建 Prompt（核心！）
            var prompt = $@"你是一个中国顶级电商运营专家，精通淘宝/拼多多/抖音详情页优化。
用户意图：{request.Intent}

原始商品信息：
标题：{request.OriginalTitle ?? "未提供"}
描述：{request.OriginalDescription ?? "未提供"}
图片数量：{request.OriginalImageUrls?.Count ?? 0}
类目：{request.Category ?? "未指定"}
目标平台：{request.TargetPlatform}

任务：
1. 优化标题和详情描述：重写标题（SEO + 高转化），描述结构化（卖点 bullet + 图文建议 + 关键词自然融入）
2. 生成营销方案：短视频脚本、种草文案、直播话术、关键卖点列表
3. 图片优化建议：为每张图生成 AI 生图 prompt（用于通义万相等模型）

输出严格 JSON 格式：
{{
  ""optimizedtitle"": ""..."",
  ""optimizeddescription"": ""... (Markdown)"",
  ""imageprompts"": [""prompt1"", ""prompt2""],
  ""marketingplan"": {{
    ""shortvideoscript"": ""..."",
    ""plantingtext"": ""..."",
    ""livescript"": ""..."",
    ""keysellingpoints"": [""点1"", ""点2""]
  }}
}}

确保中文自然、促销语气强、合规（无虚假宣传）。";

            // 调用通义千问 API（兼容 OpenAI 格式）
            var requestBody = new
            {
                model = "qwen-max",
                messages = new[]
                {
                new { role = "system", content = "你是专业电商优化助手" },
                new { role = "user", content = prompt }
            },
                temperature = 0.7,
                max_tokens = 2000
            };

            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var response = await _httpClient.PostAsJsonAsync("https://dashscope.aliyuncs.com/api/v1/services/aigc/text-generation/generation", requestBody);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var result = await response.Content.ReadFromJsonAsync<TongyiResponse>();

            // 解析 JSON 输出（假设通义返回 choices[0].message.content 是 JSON 字符串）
            var jsonContent = result?.Choices?[0]?.Message?.Content;
            if (string.IsNullOrEmpty(jsonContent))
                return BadRequest("AI 返回内容为空");

            // 简单解析（实际可加 TryParse 或 JsonSerializer）
            try
            {
                var optimized = System.Text.Json.JsonSerializer.Deserialize<OptimizeProductResponse>(jsonContent);
                return Ok(optimized);
            }
            catch
            {
                return BadRequest("AI 输出格式不正确：" + jsonContent);
            }
        }
    }

    // 通义千问响应结构（简化）
    public class TongyiResponse
    {
        public List<TongyiChoice>? Choices { get; set; }
    }

    public class TongyiChoice
    {
        public TongyiMessage? Message { get; set; }
    }

    public class TongyiMessage
    {
        public string? Content { get; set; }
    }

    // 响应 DTO：商品优化结果
    public class OptimizeProductResponse
    {
        public string? OptimizedTitle { get; set; }
        public string? OptimizedDescription { get; set; }
        public List<string>? ImagePrompts { get; set; }
        public MarketingPlan? MarketingPlan { get; set; }
    }

    public class MarketingPlan
    {
        public string? ShortVideoScript { get; set; }
        public string? PlantingText { get; set; }
        public string? LiveScript { get; set; }
        public List<string>? KeySellingPoints { get; set; }
    }
}

