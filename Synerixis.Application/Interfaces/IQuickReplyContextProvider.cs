using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public record QuickReplySnippet(string Title, string Content);

    /// <summary>供 AI 起草注入同店快捷回复上下文（限前 N 条）。</summary>
    public interface IQuickReplyContextProvider
    {
        Task<IReadOnlyList<QuickReplySnippet>> GetActiveForShopAsync(
            Guid shopId,
            int take = 8,
            CancellationToken ct = default);
    }
}
