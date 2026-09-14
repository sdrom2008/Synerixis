using System.Threading;
using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// Webhook / ConversationService 共享的入站会话入库（FindOrCreate + 买家消息）。
    /// AI 草稿 / handoff 硬闸 / 维护跳过 / 营业外策略仍仅在 Webhook ProcessInboundAiReply 路径。
    /// </summary>
    public interface IInboundSessionService
    {
        /// <summary>
        /// 按 Platform+CustomerId 查找会话；无则按 PlatformConnection(ShopId/OpenId) 建店会话，兜底临时 Seller。
        /// </summary>
        Task<ChatSession?> FindOrCreateSessionAsync(PlatformMessage msg, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刷新平台回复上下文，追加买家消息（含 PlatformMsgId），更新会话计数并落库。
        /// </summary>
        Task<ChatMessage> AppendBuyerMessageAsync(ChatSession session, PlatformMessage msg, CancellationToken cancellationToken = default);
    }
}
