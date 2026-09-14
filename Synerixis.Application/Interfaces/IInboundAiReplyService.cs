using System;
using System.Threading;
using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// Webhook / Dev 模拟进线共享的 AI 草稿路径（draft-first / handoff / 维护 / 营业外）。
    /// 缺 LLM Key 时降级本地文案，不向上抛。
    /// </summary>
    public interface IInboundAiReplyService
    {
        /// <summary>
        /// 已落库的买家消息 → Intent → Agent/LLM → Draft 或 AutoSend。
        /// 内部自建 scope，可在 fire-and-forget 中调用。
        /// </summary>
        Task ProcessAfterBuyerMessageAsync(
            Guid sessionId,
            PlatformMessage msg,
            string userContent,
            CancellationToken cancellationToken = default);
    }
}
