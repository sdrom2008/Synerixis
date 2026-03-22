using Microsoft.EntityFrameworkCore;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Repositories
{
    public class ConversationRepository : Repository<ChatSession>, IConversationRepository
    {
        public ConversationRepository(AppDbContext context) : base(context) {
        }

        public async Task<ChatSession> GetByCustomerIdAsync(string customerId)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.CustomerId == customerId);
        }

        public async Task SaveAsync(ChatSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            var entry = _context.Entry(session);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Add(session);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<ChatSession?> GetWithMessagesAsync(Guid id)
        {
            return await _dbSet
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        // The following methods are likely obsolete or need significant refactoring 
        // as they operate on the old 'Conversation' logic.
        // For now, providing a minimal implementation to satisfy the interface.

        public Task<Guid> AppendMessagesAsync(string conversationId, string sellerId, IEnumerable<ChatMessageDto> messages)
        {
            // This logic is complex and tied to the old 'Conversation' entity.
            // It needs to be rewritten to work with 'ChatSession'.
            // Returning a placeholder to allow compilation.
            Console.WriteLine("WARN: AppendMessagesAsync is not fully implemented for ChatSession.");
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<ChatContext> GetContextAsync(string conversationId, string sellerId)
        {
            // This logic is complex and tied to the old 'Conversation' entity.
            // It needs to be rewritten to work with 'ChatSession'.
            // Returning a placeholder to allow compilation.
            Console.WriteLine("WARN: GetContextAsync is not fully implemented for ChatSession.");
            return Task.FromResult(new ChatContext { Messages = new List<ChatMessageDto>() });
        }
    }
}
