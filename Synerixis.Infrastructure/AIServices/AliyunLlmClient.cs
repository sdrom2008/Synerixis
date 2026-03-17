using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Infrastructure.AI;

namespace Synerixis.Infrastructure.AIServices
{
    /// <summary>
    /// This class is the concrete implementation of the ILlmClient interface for Aliyun Tongyi Qianwen.
    /// It acts as an adapter between the Application layer's interface and the Infrastructure's Semantic Kernel service.
    /// </summary>
    public class AliyunLlmClient : ILlmClient
    {
        private readonly IChatCompletionService _chatCompletionService;

        public AliyunLlmClient(SemanticKernelService semanticKernelService)
        {
            // Get the chat service from the already-configured Semantic Kernel
            _chatCompletionService = semanticKernelService.GetChatService();
        }

        public async Task<string> GenerateTextAsync(string prompt)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);

            return result.Content ?? string.Empty;
        }
    }
}
