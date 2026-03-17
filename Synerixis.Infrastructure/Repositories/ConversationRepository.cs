using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text.Json;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Repositories
{
    public class ConversationRepository : Repository<Conversation>, IConversationRepository
    {
        public ConversationRepository(AppDbContext context) : base(context) {
        }

        // IConversationRepository implementation
        public async Task<Conversation> GetByCustomerIdAsync(string customerId)
        {
            // Note: Using Title as customer identifier (temporary workaround - domain should have CustomerId field)
            return await _dbSet
                .FirstOrDefaultAsync(c => c.Title == customerId && !c.IsDeleted);
        }

        public async Task SaveAsync(Conversation conversation)
        {
            if (conversation == null) throw new ArgumentNullException(nameof(conversation));
            var entry = _context.Entry(conversation);
            if (entry.State == EntityState.Detached)
            {
                _dbSet.Add(conversation);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<Conversation?> GetWithMessagesAsync(Guid id)
        {
            return await _dbSet
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Guid> AppendMessagesAsync(string conversationId, string sellerId, IEnumerable<ChatMessageDto> messages)
        {
            if (!Guid.TryParse(conversationId, out var convGuid))
                throw new ArgumentException("无效的 conversationId");
            if (!Guid.TryParse(sellerId, out var sellerGuid))
                throw new ArgumentException("无效的 sellerId");

            if (!messages.Any())
                throw new ArgumentException("没有消息可保存");

            // 加载 Conversation（不加载 Messages，避免跟踪旧消息）
            var conv = await _dbSet
                .FirstOrDefaultAsync(c => c.Id == convGuid && c.SellerId == sellerGuid);

            if (conv == null)
            {
                conv = Conversation.Create(sellerGuid, string.Empty);
                _dbSet.Add(conv);
                await _context.SaveChangesAsync();  // 立即保存 Conversation，确保 Id 真实
            }

            var chatMessageDbSet = _context.Set<ChatMessage>();

            // 通过聚合根添加消息（EF 自动设置 ConversationId）
            foreach (var dto in messages)
            {
                ChatMessage msg;

                if (dto.IsFromUser)
                {
                    msg = ChatMessage.FromUser(dto.Content, conv.Id);
                }
                else
                {
                    object? dataObj = dto.Data;
                    msg = ChatMessage.FromAI(content: dto.Content,messageType: dto.MessageType,data: dataObj ,conv.Id);
                }

                conv.AddMessage(msg);  // 聚合根维护关系，EF 自动设 ConversationId

                chatMessageDbSet.Add(msg);  // 直接 Add 到 DbSet
            }

            try
            {
                await _context.SaveChangesAsync();
                Console.WriteLine("保存成功 - 会话ID: " + conv.Id + ", 新消息数: " + messages.Count());
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Console.WriteLine("DbUpdateConcurrencyException: " + ex.Message);
                var entry = ex.Entries.SingleOrDefault();
                if (entry == null) throw;

                var dbValues = entry.GetDatabaseValues();
                if (dbValues == null) throw new InvalidOperationException("记录已被删除");

                entry.OriginalValues.SetValues(dbValues);
                entry.CurrentValues.SetValues(dbValues);

                await _context.SaveChangesAsync();
            }

            return conv.Id;
        }

        public async Task<ChatContext> GetContextAsync(string conversationId, string sellerId)
        {
            if (!Guid.TryParse(conversationId, out var convGuid))
                throw new ArgumentException("无效的 conversationId");

            if (!Guid.TryParse(sellerId, out var sellerGuid))
                throw new ArgumentException("无效的 sellerId");

            var conv = await _dbSet
                .AsNoTracking()
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .FirstOrDefaultAsync(c => c.Id == convGuid && c.SellerId == sellerGuid);

            if (conv == null)
            {
                return new ChatContext
                {
                    ConversationId = conversationId,
                    SellerId = sellerId,
                    Messages = new List<ChatMessageDto>()
                };
            }

            var messages = conv.Messages.Select(m => new ChatMessageDto
            {
                IsFromUser = m.IsFromUser,
                Content = m.Content,
                MessageType = m.MessageType,
                Data = string.IsNullOrEmpty(m.DataJson) ? null : JsonConvert.DeserializeObject(m.DataJson), // 用 Newtonsoft
                Timestamp = m.Timestamp
            }).ToList();

            return new ChatContext
            {
                ConversationId = conv.Id.ToString(),
                SellerId = conv.SellerId.ToString(),
                Messages = messages
            };
        }

    }
}
