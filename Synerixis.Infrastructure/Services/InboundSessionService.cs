using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using ChatMessage = Synerixis.Domain.Entities.ChatMessage;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// Webhook 与 Dev 模拟进线共享的会话入库实现。
    /// 不含 AI / handoff / 维护 / 营业外策略（仍在 Webhook ProcessInboundAiReply）。
    /// </summary>
    public class InboundSessionService : IInboundSessionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<InboundSessionService> _logger;

        public InboundSessionService(AppDbContext db, ILogger<InboundSessionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<ChatSession?> FindOrCreateSessionAsync(
            PlatformMessage msg, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(msg.CustomerId))
            {
                _logger.LogWarning("[Inbound] FindOrCreate skipped: empty CustomerId platform={Platform}", msg.Platform);
                return null;
            }

            var existing = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(
                    s => s.Platform == msg.Platform && s.CustomerId == msg.CustomerId,
                    cancellationToken);

            if (existing != null)
                return existing;

            var platformConn = await _db.Set<PlatformConnection>()
                .FirstOrDefaultAsync(pc =>
                    pc.Platform == msg.Platform &&
                    pc.IsActive &&
                    (pc.ShopId == msg.OpenId || pc.OpenId == msg.OpenId),
                    cancellationToken);

            if (platformConn != null)
            {
                var seller = await _db.Sellers.FindAsync(new object[] { platformConn.SellerId }, cancellationToken);
                if (seller != null)
                {
                    var newSession = ChatSession.Create(
                        shopId: seller.Id,
                        platform: msg.Platform,
                        customerId: msg.CustomerId!,
                        customerName: msg.CustomerName);
                    _db.ChatSessions.Add(newSession);
                    await _db.SaveChangesAsync(cancellationToken);
                    return newSession;
                }
            }

            _logger.LogWarning(
                "[Inbound] No seller for platform={Platform} openId={OpenId}; creating temporary seller",
                msg.Platform, msg.OpenId);
            var tempSeller = Seller.Create(
                openId: msg.OpenId ?? Guid.NewGuid().ToString(),
                nickname: $"{msg.Platform} Shop");
            _db.Sellers.Add(tempSeller);
            await _db.SaveChangesAsync(cancellationToken);

            var fallbackSession = ChatSession.Create(
                shopId: tempSeller.Id,
                platform: msg.Platform,
                customerId: msg.CustomerId!,
                customerName: msg.CustomerName);
            _db.ChatSessions.Add(fallbackSession);
            await _db.SaveChangesAsync(cancellationToken);
            return fallbackSession;
        }

        public async Task<ChatMessage> AppendBuyerMessageAsync(
            ChatSession session, PlatformMessage msg, CancellationToken cancellationToken = default)
        {
            session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);

            var userMsg = ChatMessage.FromUser(msg.Content ?? string.Empty, session.Id);
            userMsg.PlatformMsgId = msg.MsgId;
            session.Messages.Add(userMsg);
            session.AddUserMessage();
            await _db.SaveChangesAsync(cancellationToken);
            return userMsg;
        }
    }
}
