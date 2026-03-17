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
        private readonly IAgentRouter _agentRouter; // A router to call the correct agent
        private readonly IGeneralChatAgent _generalChatAgent; // For simple chat

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
            // 1. Find existing conversation or create a new one
            // We'll need a way to get the current SellerId, maybe from the customerId or platform context.
            // For now, we'll use a placeholder Guid.
            var sellerId = Guid.NewGuid(); // Placeholder
            
            var conversation = await _conversationRepo.GetByCustomerIdAsync(customerId)
                               ?? Conversation.Create(sellerId, platform, customerId);

            // 2. Add the new user message to the conversation history
            conversation.AddMessage(ChatMessage.FromUser(messageContent, conversation.Id));

            // 3. Classify the user's intent
            // The interface expects a string and a list of DTOs, we'll adapt to it.
            var historyDtos = conversation.Messages
                .Select(m => new ChatMessageDto { IsFromUser = m.IsFromUser, Content = m.Content })
                .ToList();
            var intent = await _intentClassifier.ClassifyAsync(messageContent, historyDtos);

            string replyContent;
            var chatContext = new ChatContext { Messages = historyDtos };

            var agent = _agentRouter.GetAgent(intent);
            if (agent != null)
            {
                var agentResult = await agent.ProcessAsync(messageContent, chatContext);
                // We'll just take the last message as the reply for now.
                replyContent = agentResult.Messages.LastOrDefault()?.Content ?? "抱歉，我暂时无法处理这个问题。";
            }
            else
            {
                replyContent = await _generalChatAgent.GenerateReplyAsync(messageContent, chatContext);
            }
            
            conversation.AddMessage(ChatMessage.FromAI(replyContent, conversationId: conversation.Id));
            await _conversationRepo.SaveAsync(conversation);

            return replyContent;
        }
    }

}
