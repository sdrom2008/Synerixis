using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Helpers;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using ChatMessage = Synerixis.Domain.Entities.ChatMessage;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// Webhook 与 Dev 模拟进线共享的会话入库实现。
    /// 不含 AI / handoff / 维护 / 营业外策略（仍在 Webhook ProcessInboundAiReply）。
    /// 新会话可按 SellerConfig.AssignmentMode 规则分流（LeastLoaded）。
    /// </summary>
    public class InboundSessionService : IInboundSessionService
    {
        private readonly AppDbContext _db;
        private readonly ISessionAssignmentService _assign;
        private readonly ILogger<InboundSessionService> _logger;

        public InboundSessionService(
            AppDbContext db,
            ISessionAssignmentService assign,
            ILogger<InboundSessionService> logger)
        {
            _db = db;
            _assign = assign;
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

            ChatSession newSession;
            if (platformConn != null)
            {
                var seller = await _db.Sellers.FindAsync(new object[] { platformConn.SellerId }, cancellationToken);
                if (seller != null)
                {
                    newSession = ChatSession.Create(
                        shopId: seller.Id,
                        platform: msg.Platform,
                        customerId: msg.CustomerId!,
                        customerName: msg.CustomerName);
                    _db.ChatSessions.Add(newSession);
                    await _db.SaveChangesAsync(cancellationToken);
                    await TryRuleAssignAsync(newSession, msg.Content, cancellationToken);
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

            newSession = ChatSession.Create(
                shopId: tempSeller.Id,
                platform: msg.Platform,
                customerId: msg.CustomerId!,
                customerName: msg.CustomerName);
            _db.ChatSessions.Add(newSession);
            await _db.SaveChangesAsync(cancellationToken);
            await TryRuleAssignAsync(newSession, msg.Content, cancellationToken);
            return newSession;
        }

        private async Task TryRuleAssignAsync(ChatSession session, string? content, CancellationToken ct)
        {
            try
            {
                // 关键词 → Supervisor（最小规则）；否则按 AssignmentMode
                var keywords = await _db.SellerConfigs.AsNoTracking()
                    .Where(c => c.SellerId == session.ShopId)
                    .Select(c => c.SensitiveKeywords)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(keywords))
                {
                    var hit = keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(k => k.Length > 0 && content.Contains(k, StringComparison.OrdinalIgnoreCase));
                    if (hit)
                    {
                        var sup = await _assign.AssignByPresetAsync(session, "supervisor", ct);
                        if (sup != null) return;
                    }
                }
                await _assign.TryAutoAssignAsync(session, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Inbound] rule assign skipped session={SessionId}", session.Id);
            }
        }

        public async Task<ChatMessage> AppendBuyerMessageAsync(
            ChatSession session, PlatformMessage msg, CancellationToken cancellationToken = default)
        {
            // 重新按 Id 加载，避免 Dev/Webhook 路径上 ChangeTracker 脏实体导致 UPDATE 0 行
            var tracked = await _db.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken);
            if (tracked == null)
                throw new InvalidOperationException($"ChatSession {session.Id} not found for AppendBuyerMessage");

            tracked.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);

            var userMsg = ChatMessage.FromUser(msg.Content ?? string.Empty, tracked.Id);
            userMsg.PlatformMsgId = msg.MsgId;
            userMsg.Metadata = MessageI18nMetadata.WithDetectedLang(
                null, CbecLanguageHelper.Detect(msg.Content));
            _db.ChatMessages.Add(userMsg);
            tracked.AddUserMessage();
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "[Inbound] AppendBuyer concurrency; retry once Session={SessionId}", tracked.Id);
                _db.ChangeTracker.Clear();
                tracked = await _db.ChatSessions.FirstAsync(s => s.Id == session.Id, cancellationToken);
                tracked.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);
                userMsg = ChatMessage.FromUser(msg.Content ?? string.Empty, tracked.Id);
                userMsg.PlatformMsgId = msg.MsgId;
                userMsg.Metadata = MessageI18nMetadata.WithDetectedLang(
                    null, CbecLanguageHelper.Detect(msg.Content));
                _db.ChatMessages.Add(userMsg);
                tracked.AddUserMessage();
                await _db.SaveChangesAsync(cancellationToken);
            }
            return userMsg;
        }
    }
}
