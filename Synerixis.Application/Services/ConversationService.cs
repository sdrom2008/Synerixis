using System;
using System.Linq;
using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Services
{
    /// <summary>
    /// 早期进线路径。生产 IM 由 WebhookController.ProcessInboundAiReplyAsync 负责
    /// （draft-first / handoff 硬闸 / 维护跳过 / 营业外 / 幂等），请勿旁路硬闸。
    /// 会话入库已与 Webhook 共用 <see cref="IInboundSessionService"/>。
    /// </summary>
    public class ConversationService : IConversationService
    {
        private readonly IInboundSessionService _inbound;
        private readonly IConversationRepository _conversationRepo;
        private readonly IIntentClassifier _intentClassifier;
        private readonly IAgentRouter _agentRouter;
        private readonly IGeneralChatAgent _generalChatAgent;

        public ConversationService(
            IInboundSessionService inbound,
            IConversationRepository conversationRepo,
            IIntentClassifier intentClassifier,
            IAgentRouter agentRouter,
            IGeneralChatAgent generalChatAgent)
        {
            _inbound = inbound;
            _conversationRepo = conversationRepo;
            _intentClassifier = intentClassifier;
            _agentRouter = agentRouter;
            _generalChatAgent = generalChatAgent;
        }

        /// <inheritdoc />
        [Obsolete("Production IM inbound uses WebhookController.ProcessInboundAiReplyAsync (draft-first/handoff). Prefer webhook path; this method shares FindOrCreate+Append via IInboundSessionService then runs a legacy AI reply (no draft/handoff gates).")]
        public async Task<string> ProcessIncomingMessageAsync(string platform, string customerId, string messageContent)
        {
            var msg = new PlatformMessage
            {
                Platform = platform ?? string.Empty,
                CustomerId = customerId,
                Content = messageContent ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            var session = await _inbound.FindOrCreateSessionAsync(msg);
            if (session == null)
                return "抱歉，无法创建会话。";

            await _inbound.AppendBuyerMessageAsync(session, msg);

            var historyDtos = session.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    IsFromUser = m.SenderType == 1,
                    Content = m.Content,
                    Timestamp = m.CreatedAt
                })
                .ToList();

            var intent = await _intentClassifier.ClassifyAsync(messageContent, historyDtos);
            var chatContext = new ChatContext
            {
                Messages = historyDtos,
                CustomerId = customerId,
                Platform = platform,
                ShopId = session.ShopId,
                ConversationId = session.Id.ToString()
            };

            string replyContent;
            var agent = _agentRouter.GetAgent(intent);
            if (agent != null)
            {
                var agentResult = await agent.ProcessAsync(messageContent, chatContext);
                replyContent = agentResult.Messages.LastOrDefault()?.Content ?? "抱歉，我暂时无法处理这个问题。";
            }
            else
            {
                replyContent = await _generalChatAgent.GenerateReplyAsync(messageContent, chatContext);
            }

            // 遗留路径：直接写 AI 消息（无草稿闸）。生产请走 Webhook DraftFirst。
            var aiMsg = ChatMessage.FromAI(replyContent, "text", null, session.Id);
            session.Messages.Add(aiMsg);
            session.AddAiMessage();
            await _conversationRepo.SaveAsync(session);

            return replyContent;
        }
    }
}
