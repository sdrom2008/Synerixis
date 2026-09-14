using Microsoft.EntityFrameworkCore;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Services
{
    public class QuickReplyContextProvider : IQuickReplyContextProvider
    {
        private readonly AppDbContext _db;

        public QuickReplyContextProvider(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<QuickReplySnippet>> GetActiveForShopAsync(
            Guid shopId,
            int take = 8,
            CancellationToken ct = default)
        {
            if (shopId == Guid.Empty || take <= 0)
                return Array.Empty<QuickReplySnippet>();

            var limit = Math.Min(take, 20);
            var items = await _db.QuickReplies.AsNoTracking()
                .Where(q => q.IsActive && (
                    (q.Scope == QuickReplyScope.Shop && q.ShopId == shopId)
                    || q.Scope == QuickReplyScope.Global))
                .OrderBy(q => q.SortOrder)
                .ThenByDescending(q => q.CreatedAt)
                .Take(limit)
                .Select(q => new { q.Title, q.Content })
                .ToListAsync(ct);

            return items
                .Select(q => new QuickReplySnippet(q.Title, Truncate(q.Content, 280)))
                .ToList();
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, max) + "…";
        }
    }
}
