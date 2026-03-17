using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces.Ai
{
    /// <summary>
    /// Defines the contract for a client that interacts with a Large Language Model.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>
        /// Generates text based on a given prompt.
        /// </summary>
        /// <param name="prompt">The input prompt for the model.</param>
        /// <returns>The AI-generated text.</returns>
        Task<string> GenerateTextAsync(string prompt);
    }
}
