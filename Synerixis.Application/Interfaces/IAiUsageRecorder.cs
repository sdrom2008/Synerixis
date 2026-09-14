using Microsoft.SemanticKernel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    /// <summary>LLM 成功调用后写入 AiUsageLog。</summary>
    public interface IAiUsageRecorder
    {
        Task RecordAsync(
            Guid sellerId,
            Guid? sessionId,
            string purpose,
            string model,
            int promptTokens,
            int completionTokens,
            bool isEstimated = false,
            CancellationToken ct = default);

        /// <summary>
        /// 从 ChatMessageContent.Metadata 尽量提取 usage；
        /// 失败则用 prompt/completion 文本 chars/4 粗估，并标记 IsEstimated。
        /// </summary>
        Task RecordFromChatResultAsync(
            Guid sellerId,
            Guid? sessionId,
            string purpose,
            string model,
            ChatMessageContent? result,
            string? promptText = null,
            string? completionText = null,
            CancellationToken ct = default);
    }
}
