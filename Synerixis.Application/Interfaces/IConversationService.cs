using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for managing customer conversations.
    /// </summary>
    public interface IConversationService
    {
        /// <summary>
        /// Processes an incoming message from an external platform (e.g., Taobao, Douyin).
        /// This is the main entry point for the AI客服 workflow.
        /// </summary>
        /// <param name="platform">The originating platform (e.g., "TAOBAO", "DOUYIN").</param>
        /// <param name="customerId">The unique ID of the customer on that platform.</param>
        /// <param name="messageContent">The content of the message from the customer.</param>
        /// <returns>The AI-generated reply message.</returns>
        Task<string> ProcessIncomingMessageAsync(string platform, string customerId, string messageContent);
    }
}
