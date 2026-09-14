using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.AI;

namespace Synerixis.Infrastructure.AIServices
{
    /// <summary>
    /// Aliyun Tongyi Qianwen adapter implementing ILlmClient.
    /// </summary>
    public class AliyunLlmClient : ILlmClient
    {
        private const string DefaultModel = "qwen-max";
        private readonly IChatCompletionService _chatCompletionService;
        private readonly IAiUsageRecorder? _usageRecorder;

        public AliyunLlmClient(SemanticKernelService semanticKernelService, IAiUsageRecorder? usageRecorder = null)
        {
            _chatCompletionService = semanticKernelService.GetChatService();
            _usageRecorder = usageRecorder;
        }

        public async Task<string> GenerateTextAsync(string prompt, LlmCallContext? usage = null)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);
            var content = result.Content ?? string.Empty;

            if (_usageRecorder != null && usage != null && usage.SellerId != Guid.Empty)
            {
                var model = string.IsNullOrWhiteSpace(usage.Model) ? DefaultModel : usage.Model!;
                var purpose = string.IsNullOrWhiteSpace(usage.Purpose) ? AiUsagePurposes.Other : usage.Purpose;
                await _usageRecorder.RecordFromChatResultAsync(
                    usage.SellerId,
                    usage.SessionId,
                    purpose,
                    model,
                    result,
                    prompt,
                    content);
            }

            return content;
        }
    }
}
