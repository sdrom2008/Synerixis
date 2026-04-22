using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Repositories
{
    /// <summary>
    /// 客服会话仓储实现
    /// </summary>
    public class ChatSessionRepository : IChatSessionRepository
    {
        private readonly AppDbContext _context;
        
        public ChatSessionRepository(AppDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// 根据买家 ID 获取会话
        /// </summary>
        public async Task<ChatSession?> GetByCustomerIdAsync(string customerId)
        {
            return await _context.ChatSessions
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }
        
        /// <summary>
        /// 根据 ID 获取会话（包含消息）
        /// </summary>
        public async Task<ChatSession?> GetByIdWithMessagesAsync(Guid id)
        {
            return await _context.ChatSessions
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        
        /// <summary>
        /// 保存会话（新加或更新）
        /// </summary>
        public async Task SaveAsync(ChatSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
                
            _context.ChatSessions.Attach(session);
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
    }
}
