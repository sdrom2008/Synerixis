using Synerixis.Application.DTOs;
using Synerixis.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public interface IIntentClassifier
    {
        /// <summary>
        /// 根据用户输入和最近对话历史，分类意图
        /// </summary>
        Task<ChatIntent> ClassifyAsync(string userInput, IReadOnlyList<ChatMessageDto> recentHistory);

        /// <summary>分类并返回粗置信度（自动 handoff 用）</summary>
        Task<IntentClassificationResult> ClassifyWithConfidenceAsync(
            string userInput,
            IReadOnlyList<ChatMessageDto> recentHistory);
    }
}
