using System;
using System.Linq;
using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces.Agents;

namespace Synerixis.Application.Services
{
    public class ConversationService : IConversationService
    {
        private readonly IConversationRepository _conversationRepo;
        private readonly IIntentClassifier _intentClassifier;
        private readonly IAgentRouter _agentRouter;
        private readonly IGeneralChatAgent _generalChatAgent;

        public ConversationService(
            IConversationRepository conversationRepo,
            IIntentClassifier intentClassifier,
            IAgentRouter agentRouter,
            IGeneralChatAgent generalChatAgent)
        {
            _conversationRepo = conversationRepo;
            _intentClassifier = intentClassifier;
            _agentRouter = agentRouter;
            _generalChatAgent = generalChatAgent;
        }

        public async Task<string> ProcessIncomingMessageAsync(string platform, string customerId, string messageContent)
        {
            var sellerId = Guid.Empty; // TODO: This must be resolved from the platform/customer context
            
            var session = await _conversationRepo.GetByCustomerIdAsync(customerId);
            if (session == null)
            {
                session = ChatSession.Create(sellerId, platform, customerId);
            }

            // Add user message and update stats
            var userMsg = ChatMessage.FromUser(messageContent, session.Id);
            session.Messages.Add(userMsg);
            session.AddUserMessage(); // This method needs to be added to ChatSession entity

            // Classify intent based on history
            var historyDtos = session.Messages
                .Select(m => new ChatMessageDto
                {
                    IsFromUser = m.SenderType == 1,  // 1 = Customer
                    Content = m.Content,
                    Timestamp = m.CreatedAt
                })
                .ToList();
            var intent = await _intentClassifier.ClassifyAsync(messageContent, historyDtos);

            string replyContent;
            var chatContext = new ChatContext { Messages = historyDtos };
            var agent = _agentRouter.GetAgent(intent);

            // Route to specific agent or general chat
            if (agent != null)
            {
                var agentResult = await agent.ProcessAsync(messageContent, chatContext);
                replyContent = agentResult.Messages.LastOrDefault()?.Content ?? "抱歉，我暂时无法处理这个问题。";
            }
            else
            {
                replyContent = await _generalChatAgent.GenerateReplyAsync(messageContent, chatContext);
            }

            // Add AI response and update stats
            var aiMsg = ChatMessage.FromAI(replyContent, "text", null, session.Id);
            session.Messages.Add(aiMsg);
            session.AddAiMessage(); // This method already exists

            // Persist changes
            await _conversationRepo.SaveAsync(session);

            return replyContent;
        }
    }
}
