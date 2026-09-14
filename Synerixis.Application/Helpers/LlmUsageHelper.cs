using System;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces.Ai;

namespace Synerixis.Application.Helpers
{
    public static class LlmUsageHelper
    {
        public static LlmCallContext? FromChatContext(ChatContext? context, string purpose)
        {
            if (context == null) return null;

            Guid sellerId = context.ShopId;
            if (sellerId == Guid.Empty && Guid.TryParse(context.SellerId, out var parsed))
                sellerId = parsed;
            if (sellerId == Guid.Empty) return null;

            Guid? sessionId = null;
            if (Guid.TryParse(context.ConversationId, out var sid))
                sessionId = sid;

            return new LlmCallContext
            {
                SellerId = sellerId,
                SessionId = sessionId,
                Purpose = purpose
            };
        }
    }
}
