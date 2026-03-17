using System.Threading.Tasks;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces;
using Synerixis.Application.DTOs;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for a repository that handles conversation data.
    /// </summary>
    public interface IConversationRepository : IRepository<Conversation>
    {
        Task<Conversation> GetByCustomerIdAsync(string customerId);

        Task<ChatContext> GetContextAsync(string conversationId, string sellerId);

        Task<Guid> AppendMessagesAsync(string conversationId, string sellerId, IEnumerable<ChatMessageDto> messages);

        Task SaveAsync(Conversation conversation);
    }
}
