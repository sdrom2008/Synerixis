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

        public async Task<string> GenerateTextAsync(string prompt)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await _chatCompletionService.GetChatMessageContentAsync(chatHistory);

            // 无 seller 上下文时记不到店维度；调用方若有店应走带上下文的路径
            if (_usageRecorder != null)
            {
                await _usageRecorder.RecordFromChatResultAsync(
                    Guid.Empty, null, AiUsagePurposes.Other, DefaultModel, result);
            }

            return result.Content ?? string.Empty;
        }
    }
}
