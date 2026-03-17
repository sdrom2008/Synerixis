using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces.Agents
{
    /// <summary>
    /// Defines the contract for an agent that can process a user's intent.
    /// </summary>
    public interface IAgent
    {
        /// <summary>
        /// The unique name of the intent this agent can handle (e.g., "QueryOrder").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Processes the conversation to fulfill the user's intent.
        /// </summary>
        Task<AgentResult> ProcessAsync(Conversation conversation);
    }

    /// <summary>
    /// Represents the result of an agent's execution.
    /// </summary>
    /// <param name="AgentName">The name of the agent that produced the result.</param>
    /// <param name="ResponseMessage">The response message to be sent to the user.</param>
    /// <param name="IsSuccess">Indicates whether the agent executed successfully.</param>
    public record AgentResult(string AgentName, string ResponseMessage, bool IsSuccess);
}
