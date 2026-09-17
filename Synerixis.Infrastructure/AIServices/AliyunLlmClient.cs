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
    /// OpenAI-compatible ILlmClient（经 LlmRuntime：Admin Provider / appsettings / 商家 Key）。
    /// </summary>
    public class AliyunLlmClient : ILlmClient
    {
        private readonly LlmRuntime _runtime;
        private readonly IAiUsageRecorder? _usageRecorder;

        public AliyunLlmClient(LlmRuntime runtime, IAiUsageRecorder? usageRecorder = null)
        {
            _runtime = runtime;
            _usageRecorder = usageRecorder;
        }

        public async Task<string> GenerateTextAsync(string prompt, LlmCallContext? usage = null)
        {
            var chat = _runtime.GetChatService();
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await chat.GetChatMessageContentAsync(chatHistory);
            var content = result.Content ?? string.Empty;

            if (_usageRecorder != null && usage != null && usage.SellerId != Guid.Empty)
            {
                var model = string.IsNullOrWhiteSpace(usage.Model) ? _runtime.ModelId : usage.Model!;
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
