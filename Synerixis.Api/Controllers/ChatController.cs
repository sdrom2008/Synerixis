using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Synerixis.Api.Extensions;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Repositories;
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/chat")]
    [Authorize]  // 所有接口都需要登录（Seller 身份）
    public class ChatController : BaseApiController
    {
        private readonly IAiChatService _aiChatService;
        private readonly IRepository<Conversation> _conversationRepository;

        public ChatController(
            IAiChatService aiChatService,
            IRepository<Conversation> conversationRepository)
        {
            _aiChatService = aiChatService ?? throw new ArgumentNullException(nameof(aiChatService));
            _conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
        }

        /// <summary>
        /// 发送聊天消息（支持商品优化、营销方案、智能客服等意图）
        /// </summary>
        /// <param name="command">聊天输入（消息内容 + 可选会话ID）</param>
        /// <returns>聊天回复（包含结构化结果，如商品优化数据）</returns>
        [HttpPost("send")]
        [ProducesResponseType(typeof(ChatMessageReplyDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendMessage([FromBody] SendChatMessageCommand command)
        {
            // 1. 参数校验
            if (command == null || string.IsNullOrWhiteSpace(command.Message))
            {
                return BadRequest(new { message = "消息内容不能为空" });
            }

            // 2. 获取当前商家 ID
            Guid sellerId;
            try
            {
                sellerId = User.GetSellerId();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("无效的商家身份");
            }

            // 3. 不要在这里生成新 Guid，直接传原值（null 或 Empty 表示新建）
            Guid? conversationId = command.ConversationId;  // 保持前端传的
            string message = command.Message.Trim();
            var extraData = command.ExtraData;

            // 4. 调用核心服务（4 参数版本）
            ChatMessageReplyDto reply;
            try
            {
                reply = await _aiChatService.ProcessUserMessageAsync(
                    sellerId,
                    conversationId,
                    message,
                    extraData);
            }
         catch (Exception)
            {
                // 可加日志
                // _logger.LogError(ex, "处理消息失败: SellerId={SellerId}", sellerId);
                return StatusCode(500, new { message = "服务器内部错误，请稍后再试" });
            }
            //var reply = await _aiChatService.ProcessUserMessageAsync(sellerId, command.ConversationId, command.Message,command.ExtraData);

            // 4. 返回结构化结果
            return Ok(reply);
        }

        /// <summary>
        /// 获取当前商家的所有会话列表
        /// </summary>
        //[HttpGet("conversations")]
        //public async Task<IActionResult> GetConversations()
        //{
        //    Guid sellerId;
        //    try
        //    {
        //        sellerId = User.GetSellerId();
        //    }
        //    catch (UnauthorizedAccessException)
        //    {
        //        return Unauthorized("无效的商家身份");
        //    }

        //    var conversations = await _conversationRepository.GetAllAsync(
        //        c => c.SellerId == sellerId && !c.IsDeleted,
        //        orderBy: q => q.OrderByDescending(c => c.LastActiveAt));

        //    var result = conversations.Select(c => new
        //    {
        //        id = c.Id,
        //        title = c.Title,
        //        lastMessage = c.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()?.Content ?? "暂无消息",
        //        lastActiveAt = c.LastActiveAt?.ToString("yyyy-MM-dd HH:mm")
        //    });

        //    return Ok(result);
        //}

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            Guid sellerId = User.GetSellerId();

            var query = _conversationRepository.GetQueryable(
                c => c.SellerId == sellerId && !c.IsDeleted)
                .OrderByDescending(c => c.LastActiveAt);

            var total = await query.CountAsync();

            var conversations = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = conversations.Select(c => new
            {
                id = c.Id,
                title = c.Title,
                lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.Content ?? "暂无消息",
                lastActiveAt = c.LastActiveAt?.ToString("yyyy-MM-dd HH:mm")
            });

            return Ok(new
            {
                items = result,
                total,
                page,
                pageSize,
                hasMore = result.Count() == pageSize
            });
        }

        /// <summary>
        /// 获取指定会话的完整历史消息（用于前端加载历史聊天记录）
        /// </summary>
        /// <param name="id">会话ID</param>
        [HttpGet("conversation/{id}")]
        public async Task<IActionResult> GetConversationMessages(Guid id)
        {
            Guid sellerId;
            try
            {
                sellerId = User.GetSellerId();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("无效的商家身份");
            }

            var conversation = await _conversationRepository.FirstOrDefaultAsync(
                c => c.Id == id && c.SellerId == sellerId,
                include: q => q.Include(c => c.Messages));

            if (conversation == null)
            {
                return NotFound(new { message = "会话不存在" });
            }

            var messages = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    isFromUser = m.SenderType == 1,
                    content = m.Content,
                    messageType = m.MessageType == 1 ? "text" : "other",
                    data = m.Metadata != null
                        ? JsonSerializer.Deserialize<object>(m.Metadata)
                        : null
                });

            return Ok(new
            {
                conversationId = conversation.Id.ToString(),
                title = conversation.Title,
                messages
            });
        }

        /// <summary>
        /// 接口更新查看时间
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPatch("conversation/{id}/view")]
        public async Task<IActionResult> MarkConversationViewed(Guid id)
        {
            Guid sellerId;
            try
            {
                sellerId = User.GetSellerId();
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("无效的商家身份");
            }

            var conversation = await _conversationRepository.FirstOrDefaultAsync(
                c => c.Id == id && c.SellerId == sellerId);

            if (conversation == null)
            {
                return NotFound("会话不存在");
            }

            conversation.MarkAsViewed();
            await _conversationRepository.SaveChangesAsync();  // ← 这里调用仓储的 SaveChanges

            return Ok();
        }

        /// <summary>
        /// 删除会话
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("conversation/{id}")]
        public async Task<IActionResult> DeleteConversation(Guid id)
        {
            Guid sellerId = User.GetSellerId();

            var conversation = await _conversationRepository.FirstOrDefaultAsync(
                c => c.Id == id && c.SellerId == sellerId && !c.IsDeleted);

            if (conversation == null)
            {
                return NotFound("会话不存在");
            }

            // 标记隐藏（加一个 IsDeleted 字段）
            conversation.MarkAsDeleted();
            await _conversationRepository.SaveChangesAsync();

            return Ok();
        }
    }
}