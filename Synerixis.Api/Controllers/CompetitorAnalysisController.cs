using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Synerixis.Application.Interfaces;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Enums;
using System.Security.Claims;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/competitor")]
    [Authorize]  // 需要登录（JWT）
    public class CompetitorAnalysisController : ControllerBase
    {
        private readonly IAgentRouter _agentRouter;

        public CompetitorAnalysisController(IAgentRouter agentRouter)
        {
            _agentRouter = agentRouter;
        }

        /// <summary>
        /// 竞品分析：输入关键词，输出市场分析报告
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] CompetitorAnalysisRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Keyword))
                return BadRequest("关键词不能为空");

            // 构造 ChatContext（ShopId 用于 AiUsageLog）
            var sellerClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("shopId")?.Value
                ?? User.FindFirst("sellerId")?.Value
                ?? "";
            Guid.TryParse(sellerClaim, out var shopId);
            var context = new ChatContext
            {
                ConversationId = Guid.NewGuid().ToString(),
                SellerId = sellerClaim,
                ShopId = shopId,
                Platform = request.Platform ?? "",
                ProductName = request.Keyword
            };

            // 通过 AgentRouter 路由到 CompetitorAnalysisAgent
            var result = await _agentRouter.RouteAsync(ChatIntent.CompetitorAnalysis, request.Keyword, context);

            if (!result.Success)
                return StatusCode(500, result.ErrorMessage ?? "分析失败");

            // 提取 AI 回复内容
            var message = result.Messages.FirstOrDefault();
            if (message == null)
                return StatusCode(500, "未生成分析结果");

            var response = new CompetitorAnalysisResponse
            {
                Report = message.Content,
                GeneratedAt = message.Timestamp
            };

            return Ok(response);
        }
    }

    public class CompetitorAnalysisRequest
    {
        public string Keyword { get; set; } = string.Empty;
        public string? Platform { get; set; } // e.g., "taobao", "douyin"
    }

    public class CompetitorAnalysisResponse
    {
        public string Report { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}
