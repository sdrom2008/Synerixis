using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synerixis.Application.DTOs;
using Synerixis.Application.Helpers;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;
using ChatMessageEntity = Synerixis.Domain.Entities.ChatMessage;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// 与 WebhookController 原 ProcessInboundAiReplyAsync 等价：draft-first / handoff 闸门保留。
    /// </summary>
    public class InboundAiReplyService : IInboundAiReplyService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InboundAiReplyService> _logger;

        public InboundAiReplyService(
            IServiceScopeFactory scopeFactory,
            ILogger<InboundAiReplyService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessAfterBuyerMessageAsync(
            Guid sessionId,
            PlatformMessage msg,
            string userContent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                var db = sp.GetRequiredService<AppDbContext>();
                var logger = sp.GetRequiredService<ILogger<InboundAiReplyService>>();

                var session = await db.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
                if (session == null)
                {
                    logger.LogWarning("[InboundAi] Session {SessionId} missing for AI reply", sessionId);
                    return;
                }

                var opsSvc = sp.GetRequiredService<ISystemSettingsService>();
                var opsNow = await opsSvc.GetOpsAsync();
                if (opsNow.MaintenanceMode)
                {
                    logger.LogInformation(
                        "[InboundAi] MaintenanceMode: skip AI draft/AutoSend Session={SessionId}",
                        sessionId);
                    return;
                }

                var sellerConfig = await db.SellerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.SellerId == session.ShopId, cancellationToken);
                if (sellerConfig != null && !sellerConfig.EnableAutoReply)
                {
                    logger.LogInformation(
                        "[InboundAi] AI draft/reply disabled for shop {ShopId}; skipping",
                        session.ShopId);
                    return;
                }

                if (session.BlocksAiDrafting)
                {
                    logger.LogInformation(
                        "[InboundAi] Handoff hard-gate: skip AI draft/AutoSend Session={SessionId} Shop={ShopId}",
                        sessionId, session.ShopId);
                    session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                }

                session.UpdatePlatformReplyContext(msg.ConversationId, msg.OpenId);

                var sensitiveHit = OutboundPolicyHelper.FindSensitiveHit(
                    userContent, sellerConfig?.SensitiveKeywords);
                if (sensitiveHit != null)
                {
                    session.TransferToAgent();
                    await SupersedePendingDraftsAsync(db, session.Id, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogWarning(
                        "[InboundAi] Auto-handoff by sensitive keyword '{Keyword}' Session={SessionId} Shop={ShopId}",
                        sensitiveHit, sessionId, session.ShopId);
                    return;
                }

                var withinHours = OutboundPolicyHelper.IsWithinBusinessHours(
                    sellerConfig?.BusinessHoursStart,
                    sellerConfig?.BusinessHoursEnd,
                    sellerConfig?.TimeZoneId);
                var handoffOutside = sellerConfig?.HandoffOutsideBusinessHours ?? true;
                if (!withinHours)
                {
                    logger.LogInformation(
                        "[InboundAi] Outside business hours Shop={ShopId} Session={SessionId} Hours={Start}-{End} Tz={Tz}",
                        session.ShopId, sessionId,
                        sellerConfig?.BusinessHoursStart ?? "09:00",
                        sellerConfig?.BusinessHoursEnd ?? "22:00",
                        sellerConfig?.TimeZoneId ?? "Asia/Shanghai");

                    if (handoffOutside)
                    {
                        session.TransferToAgent();
                        await SupersedePendingDraftsAsync(db, session.Id, cancellationToken);
                        var tip = new DraftMessage
                        {
                            Id = Guid.NewGuid(),
                            ChatSessionId = session.Id,
                            Content = "【营业外】非营业时间，请人工稍后回复。",
                            Status = DraftStatuses.Pending,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.DraftMessages.Add(tip);
                        await db.SaveChangesAsync(cancellationToken);
                        logger.LogInformation(
                            "[InboundAi] Outside-hours handoff Session={SessionId}; tip draft saved (no AI / no AutoSend)",
                            sessionId);
                        return;
                    }
                }

                var outboundMode = OutboundModes.Normalize(sellerConfig?.OutboundMode);
                var allowAutoSend = OutboundModes.IsAutoSend(outboundMode)
                    && !session.PendingHumanHandoff
                    && withinHours; // 新建会话亦为 Pending，不能用 Status 挡 AutoSend；停 AI 看 PendingHumanHandoff

                var historyDtos = session.Messages
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageDto
                    {
                        IsFromUser = m.SenderType == 1,
                        Content = m.Content,
                        MessageType = m.MessageType == 1 ? "text" : "other",
                        Timestamp = m.CreatedAt
                    })
                    .ToList();

                var chatContext = new ChatContext
                {
                    ConversationId = session.Id.ToString(),
                    ShopId = session.ShopId,
                    SellerId = session.ShopId.ToString(),
                    Platform = session.Platform,
                    CustomerId = session.CustomerId ?? msg.CustomerId ?? string.Empty,
                    PlatformShopId = msg.OpenId,
                    Messages = historyDtos
                };

                ChatIntent intent = ChatIntent.Unknown;
                double confidence = 0.15;
                try
                {
                    var classifier = sp.GetRequiredService<IIntentClassifier>();
                    var classified = await classifier.ClassifyWithConfidenceAsync(
                        userContent, historyDtos, session.ShopId, session.Id);
                    intent = classified.Intent;
                    confidence = classified.Confidence;
                    logger.LogInformation(
                        "[InboundAi] Intent={Intent} Confidence={Confidence:F2} Session={SessionId} Platform={Platform}",
                        intent, confidence, sessionId, session.Platform);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[InboundAi] Intent classify failed (missing AI key?); fallback Unknown");
                    intent = ChatIntent.Unknown;
                    confidence = 0.15;
                }

                var autoLow = sellerConfig?.AutoHandoffOnLowConfidence ?? true;
                var threshold = sellerConfig?.HandoffConfidenceThreshold ?? 0.45;
                if (threshold < 0) threshold = 0;
                if (threshold > 1) threshold = 1;
                if (autoLow && confidence < threshold)
                {
                    session.TransferToAgent();
                    await SupersedePendingDraftsAsync(db, session.Id, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogWarning(
                        "[InboundAi] Auto-handoff by low confidence {Confidence:F2}<{Threshold:F2} Intent={Intent} Session={SessionId} Shop={ShopId}",
                        confidence, threshold, intent, sessionId, session.ShopId);
                    return;
                }

                string replyContent;
                try
                {
                    var agentRouter = sp.GetRequiredService<IAgentRouter>();
                    var agent = agentRouter.GetAgent(intent);

                    if (agent != null && intent is not (ChatIntent.GeneralChat or ChatIntent.Unknown))
                    {
                        var agentResult = await agent.ProcessAsync(userContent, chatContext);
                        replyContent = agentResult.Messages.LastOrDefault()?.Content
                            ?? "抱歉，我暂时无法处理您的问题。";
                    }
                    else
                    {
                        var generalChat = sp.GetRequiredService<IGeneralChatAgent>();
                        replyContent = await generalChat.GenerateReplyAsync(userContent, chatContext);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[InboundAi] Agent/LLM reply failed; using local fallback");
                    replyContent = "您好！您的消息已收到，我们会尽快为您处理。";
                }

                if (!withinHours)
                    replyContent = "【营业外】" + replyContent;

                await SupersedePendingDraftsAsync(db, session.Id, cancellationToken);

                if (allowAutoSend)
                {
                    var aiMsg = ChatMessageEntity.FromAI(replyContent, chatSessionId: session.Id);
                    session.Messages.Add(aiMsg);
                    session.AddAiMessage();
                    await db.SaveChangesAsync(cancellationToken);

                    try
                    {
                        var platformRouter = sp.GetRequiredService<IPlatformClientRouter>();
                        var client = platformRouter.GetClient(msg.Platform);
                        var platformOutboundId = await client.SendReplyAsync(msg, replyContent);
                        aiMsg.PlatformMsgId = !string.IsNullOrWhiteSpace(platformOutboundId)
                            ? platformOutboundId
                            : $"outbound:{aiMsg.Id:N}";
                        await db.SaveChangesAsync(cancellationToken);
                        logger.LogWarning(
                            "[InboundAi] AutoSend used for shop {ShopId} platform {Platform} — compliance risk; prefer DraftFirst",
                            session.ShopId, msg.Platform);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "[InboundAi] SendReply skipped/failed for {Platform} (missing token/config?)",
                            msg.Platform);
                    }
                }
                else
                {
                    var draft = new DraftMessage
                    {
                        Id = Guid.NewGuid(),
                        ChatSessionId = session.Id,
                        Content = replyContent,
                        Status = DraftStatuses.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.DraftMessages.Add(draft);
                    await db.SaveChangesAsync(cancellationToken);
                    logger.LogInformation(
                        "[InboundAi] Draft saved Session={SessionId} DraftId={DraftId} Mode={Mode} OutsideHours={Outside} (no SendReply)",
                        sessionId, draft.Id, outboundMode, !withinHours);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InboundAi] Async reply failed for session {SessionId}", sessionId);
            }
        }

        private static async Task SupersedePendingDraftsAsync(
            AppDbContext db, Guid sessionId, CancellationToken cancellationToken)
        {
            var oldDrafts = await db.DraftMessages
                .Where(d => d.ChatSessionId == sessionId && d.Status == DraftStatuses.Pending)
                .ToListAsync(cancellationToken);
            foreach (var d in oldDrafts)
            {
                d.Status = DraftStatuses.Superseded;
                d.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
