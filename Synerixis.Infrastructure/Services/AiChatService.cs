using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Services
{
    public class AiChatService : IAiChatService
    {
        private readonly IIntentClassifier _intentClassifier;
        private readonly IGeneralChatAgent _generalChatAgent;
        private readonly IAgentRouter _agentRouter;
        private readonly IConversationRepository _conversationRepository;
        private readonly ILogger<AiChatService> _logger;
        private readonly IRepository<Seller> _sellerRepository;

        public AiChatService(IIntentClassifier intentClassifier, IAgentRouter agentRouter, IGeneralChatAgent generalChatAgent, IConversationRepository conversationRepository, ILogger<AiChatService> logger, IRepository<Seller> sellerRepository)
        {
            _intentClassifier = intentClassifier;
            _agentRouter = agentRouter;
            _generalChatAgent = generalChatAgent;
            _conversationRepository = conversationRepository;
            _logger = logger;
            _sellerRepository = sellerRepository;
        }

        public async Task<ChatMessageReplyDto> ProcessUserMessageAsync(Guid sellerId, Guid? conversationId, string message, Dictionary<string, string>? extraData = null)
        {
            var seller = await _sellerRepository.GetByIdAsync(sellerId);
            if (seller == null) throw new UnauthorizedAccessException("商户不存在");

            if (seller.FreeQuota <= 0 && (seller.SubscriptionEnd == null || seller.SubscriptionEnd < DateTime.UtcNow))
            {
                throw new InvalidOperationException("免费额度已用完，请续费");
            }
            seller.ConsumeQuota();
            await _sellerRepository.UpdateAsync(seller);
            await _sellerRepository.SaveChangesAsync();

            _logger.LogInformation("Processing message for SellerId: {SellerId}, ConvId: {ConvId}", sellerId, conversationId);

            ChatSession session;
            if (conversationId == null || conversationId == Guid.Empty)
            {
                _logger.LogInformation("Creating new ChatSession for seller {SellerId}", sellerId);
                // Assuming customerId is derived from user or is a placeholder for direct seller interaction
                string customerIdForSession = $"seller_{sellerId}";
                session = ChatSession.Create(sellerId, "WebApp", customerIdForSession, seller.Nickname);
                // The repository will handle adding it if it's new
            }
            else
            {
                session = await _conversationRepository.GetByIdAsync(conversationId.Value);
                if (session == null || session.ShopId != sellerId)
                {
                    throw new UnauthorizedAccessException("会话不存在或无权访问");
                }
            }

            var userMsg = Synerixis.Domain.Entities.ChatMessage.FromUser(message, session.Id);
            session.Messages.Add(userMsg);
            session.AddUserMessage();

            var historyDtos = session.Messages.Select(m => new ChatMessageDto
            {
                IsFromUser = m.SenderType == 1,  // SenderType 1 = Customer
                Content = m.Content,
                MessageType = m.MessageType == 1 ? "text" : "other",
                Timestamp = m.CreatedAt
            }).ToList();
            var intent = await _intentClassifier.ClassifyWithConfidenceAsync(
                message, historyDtos, sellerId, session.Id);

            string replyContent;
            var chatContext = new ChatContext
            {
                Messages = historyDtos,
                ShopId = sellerId,
                SellerId = sellerId.ToString(),
                ConversationId = session.Id.ToString()
            };
            var agent = _agentRouter.GetAgent(intent.Intent);

            if (agent != null)
            {
                var agentResult = await agent.ProcessAsync(message, chatContext);
                replyContent = agentResult.Messages.LastOrDefault()?.Content ?? "抱歉，我暂时无法处理您的问题。";
            }
            else
            {
                replyContent = await _generalChatAgent.GenerateReplyAsync(message, chatContext);
            }

            var aiMsg = Synerixis.Domain.Entities.ChatMessage.FromAI(replyContent, "text", null, session.Id);
            session.Messages.Add(aiMsg);
            session.AddAiMessage();

            //await _conversationRepository.SaveAsync(session);
            //_logger.LogInformation("Session {SessionId} saved with new messages.", session.Id);
            try
            {
                await _conversationRepository.SaveAsync(session);
                _logger.LogInformation("Session {SessionId} saved with new messages.", session.Id);
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysqlEx)
            {
                _logger.LogError(ex, "MySQL 保存失败 - ErrorCode: {ErrorCode}, Message: {Message}, SQL State: {SqlState}",
                    mysqlEx.Number, mysqlEx.Message, mysqlEx.SqlState);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存 session 失败，SessionId: {SessionId}", session.Id);
                throw;
            }

            return new ChatMessageReplyDto
            {
                ConversationId = session.Id,
                MessageId = aiMsg.Id,
                Content = aiMsg.Content,
                MessageType = aiMsg.MessageType == 1 ? "text" : "other"
            };
        }

        public Task<SynerixisResponse> HandleMessageAsync(string conversationId, string userInput, string sellerId, CancellationToken ct = default)
        {
             // This implementation seems obsolete compared to ProcessUserMessageAsync.
             // Leaving it as not implemented to avoid confusion.
            _logger.LogWarning("Obsolete method HandleMessageAsync was called.");
            throw new NotImplementedException("This method is obsolete. Use ProcessUserMessageAsync instead.");
        }
    }
}
