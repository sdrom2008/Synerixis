using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Repositories
{
    /// <summary>
    /// 客服会话仓储实现 - 基于 ChatSession 实体
    /// </summary>
    public class ConversationRepository : Repository<ChatSession>, IConversationRepository
    {
        private readonly AppDbContext _context;

        public ConversationRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// 根据买家 ID 获取会话
        /// </summary>
        public async Task<ChatSession> GetByCustomerIdAsync(string customerId)
        {
            var session = await _context.ChatSessions
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
            return session ?? throw new Exception("会话不存在");
        }

        /// <summary>
        /// 保存会话
        /// </summary>
        public async Task SaveAsync(ChatSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var entry = _context.Entry(session);
            if (entry.State == EntityState.Detached)
            {
                _context.ChatSessions.Add(session);
            }
            else
            {
                _context.ChatSessions.Update(session);
            }
            
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 根据 ID 获取会话（包含消息）
        /// </summary>
        public async Task<ChatSession?> GetWithMessagesAsync(Guid id)
        {
            return await _context.ChatSessions
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>
        /// 获取会话上下文（基于真实 ChatSession）
        /// </summary>
        public async Task<ChatContext> GetContextAsync(string conversationId, string sellerId)
        {
            var session = await GetWithMessagesAsync(Guid.Parse(conversationId));
            if (session == null)
                throw new Exception("会话不存在");
                
            var messages = session.Messages.Select(m => new ChatMessageDto
            {
                Content = m.Content,
                IsFromUser = m.IsFromUser,
                MessageType = m.MessageType.ToString(),
                Timestamp = m.Timestamp
            }).ToList();

            return new ChatContext
            {
                Messages = messages,
                CustomerId = session.CustomerId,
                Platform = session.Platform,
                ShopId = session.ShopId
            };
        }

        /// <summary>
        /// 追加消息到会话
        /// </summary>
        public async Task<Guid> AppendMessagesAsync(string conversationId, string sellerId, IEnumerable<ChatMessageDto> messages)
        {
            var session = await GetWithMessagesAsync(Guid.Parse(conversationId));
            if (session == null)
                throw new Exception("会话不存在");

            foreach (var msgDto in messages)
            {
                var message = ChatMessage.FromUser(msgDto.Content, session.Id);
                if (!msgDto.IsFromUser)
                    message.SenderType = 2;  // Agent
                if (msgDto.MessageType != "text")
                    message.MessageType = int.TryParse(msgDto.MessageType, out var mt) ? mt : 1;
                session.Messages.Add(message);
            }

            await SaveAsync(session);
            return session.Id;
        }
    }
}
