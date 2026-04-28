using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Api.Helpers;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 商户端接口（查看会话、转人工、绑定平台）
    /// </summary>
    [Authorize]
    [Route("api/merchant")]
    public class MerchantController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IRepository<ChatSession> _sessionRepo;
        private readonly IMerchantPlatformService _platformService;
        private readonly IPlatformConnectionRepository _connectionRepo;

        public MerchantController(
            AppDbContext db, 
            IRepository<ChatSession> sessionRepo,
            IMerchantPlatformService platformService,
            IPlatformConnectionRepository connectionRepo)
        {
            _db = db;
            _sessionRepo = sessionRepo;
            _platformService = platformService;
            _connectionRepo = connectionRepo;
        }

        /// <summary>
        /// 获取可绑定的平台列表
        /// </summary>
        [HttpGet("platforms")]
        public async Task<IActionResult> GetPlatforms()
        {
            var platforms = await _platformService.GetAvailablePlatformsAsync();
            return Ok(new { items = platforms });
        }

        /// <summary>
        /// 获取指定平台的授权 URL
        /// </summary>
        [HttpGet("bind/{platform}")]
        public async Task<IActionResult> GetBindUrl(string platform)
        {
            var state = GenerateState();
            var url = await _platformService.GetAuthorizationUrlAsync(platform, state);
            return Ok(new { url, state });
        }

        /// <summary>
        /// 绑定平台店铺（处理 OAuth 回调）
        /// </summary>
        [HttpPost("bind/callback")]
        public async Task<IActionResult> BindCallback([FromBody] BindCallbackRequest request)
        {
            var sellerId = GetCurrentSellerId();
            var result = await _platformService.BindShopAsync(request.Platform, request.Code, sellerId);
            
            if (result.Success)
            {
                return Ok(new { message = "绑定成功", result });
            }
            return BadRequest(new { message = result.Error });
        }

        /// <summary>
        /// 获取已绑定的店铺列表
        /// </summary>
        [HttpGet("connections")]
        public async Task<IActionResult> GetConnections()
        {
            var sellerId = GetCurrentSellerId();
            var connections = await _connectionRepo.GetBySellerIdAndActiveAsync(sellerId);
            
            var items = connections.Select(c => new 
            { 
                c.Id, 
                c.Platform, 
                c.ShopId, 
                c.Nickname, 
                c.AvatarUrl,
                c.IsActive 
            });
            
            return Ok(new { items });
        }

         /// <summary>
        /// 解绑店铺
        /// </summary>
        [HttpPost("unbind/{platform}")]
        public async Task<IActionResult> Unbind(string platform)
        {
            var sellerId = GetCurrentSellerId();
            var connection = await _connectionRepo.GetBySellerIdAsync(sellerId);
            if (connection == null)
                return NotFound("未找到绑定记录");

            await _platformService.UnbindShopAsync(platform, connection.Id);
            return Ok(new { message = "已解绑" });
        }

        /// <summary>
        /// 获取本店的所有会话列表
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] string? status = null)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var query = _db.ChatSessions
                    .Include(s => s.AssignedAgent)
                    .Where(s => s.ShopId == shopId);

                if (!string.IsNullOrEmpty(status))
                {
                    if (Enum.TryParse<SessionStatus>(status, true, out var sessionStatus))
                    {
                        query = query.Where(s => s.Status == sessionStatus);
                    }
                    else
                    {
                        return BadRequest(new { message = "无效的状态参数" });
                    }
                }

                query = query
                    .OrderByDescending(s => s.CreatedAt);

                var total = await query.CountAsync();
                var items = await query
                    .Select(s => new
                    {
                        id = s.Id,
                        sessionId = s.SessionId,
                        customerName = s.CustomerName,
                        platform = s.Platform,
                        status = ((SessionStatus)s.Status).ToString(),
                        priority = ((SessionPriority)s.Priority).ToString(),
                        assignedAgent = s.AssignedAgent != null ? new
                        {
                            s.AssignedAgent.Id,
                            s.AssignedAgent.Name
                        } : null,
                        assignedAt = s.AssignedAt,
                        createdAt = s.CreatedAt,
                        lastActiveAt = s.LastActiveAt,
                        messageCount = s.MessageCount,
                        aiMessageCount = s.AiMessageCount,
                        agentMessageCount = s.AgentMessageCount
                    })
                    .ToListAsync();

                return Ok(new { items, total });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 获取指定会话的详细消息（只读）
        /// </summary>
        [HttpGet("sessions/{id}/messages")]
        public async Task<IActionResult> GetSessionMessages(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                var messages = await _db.ChatMessages
                    .Where(m => m.ChatSessionId == id)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new
                    {
                        id = m.Id,
                        content = m.Content,
                        senderType = m.SenderType == 1 ? "Customer" : (m.SenderType == 2 ? "Agent" : "System"),
                        messageType = m.MessageType == 1 ? "text" : "other",
                        metadata = m.Metadata != null ? m.Metadata : null,
                        createdAt = m.CreatedAt
                    })
                    .ToListAsync();

                return Ok(messages);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>
        /// 商户手动将会话转人工（标记为 Pending 并解除分配）
        /// </summary>
        [HttpPost("sessions/{id}/transfer")]
        public async Task<IActionResult> TransferToAgent(Guid id)
        {
            try
            {
                var shopId = GetCurrentSellerShopId();

                var session = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shopId);
                if (session == null)
                    return NotFound(new { message = "会话不存在" });

                // 只有非 Closed 的会话才能转人工
                if (session.Status == SessionStatus.Closed)
                    return BadRequest(new { message = "已关闭的会话不能转人工" });

                session.TransferToAgent();
                await _db.SaveChangesAsync();

                return Ok(new { message = "已转人工，等待客服接入" });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        private string GenerateState()
        {
            var buffer = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buffer);
            return Convert.ToBase64String(buffer);
        }
    }

    public class BindCallbackRequest
    {
        public string Platform { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}