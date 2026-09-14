using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Services
{
    public class AiUsageRecorder : IAiUsageRecorder
    {
        // 粗费率（USD / 1M tokens），仅估算；无 token 时费用为 0
        private const decimal PromptUsdPer1M = 0.40m;
        private const decimal CompletionUsdPer1M = 1.20m;

        private readonly AppDbContext _db;
        private readonly ILogger<AiUsageRecorder> _logger;

        public AiUsageRecorder(AppDbContext db, ILogger<AiUsageRecorder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task RecordAsync(
            Guid sellerId,
            Guid? sessionId,
            string purpose,
            string model,
            int promptTokens,
            int completionTokens,
            CancellationToken ct = default)
        {
            if (sellerId == Guid.Empty) return;

            var prompt = Math.Max(0, promptTokens);
            var completion = Math.Max(0, completionTokens);
            var cost = EstimateCost(prompt, completion);

            try
            {
                _db.AiUsageLogs.Add(new AiUsageLog
                {
                    Id = Guid.NewGuid(),
                    SellerId = sellerId,
                    SessionId = sessionId,
                    Model = string.IsNullOrWhiteSpace(model) ? "unknown" : model.Trim(),
                    PromptTokens = prompt,
                    CompletionTokens = completion,
                    EstimatedCostUsd = cost,
                    CreatedAt = DateTime.UtcNow,
                    Purpose = string.IsNullOrWhiteSpace(purpose) ? AiUsagePurposes.Other : purpose.Trim().ToLowerInvariant()
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AiUsage] Failed to persist usage log Seller={SellerId} Purpose={Purpose}",
                    sellerId, purpose);
            }
        }

        public Task RecordFromChatResultAsync(
            Guid sellerId,
            Guid? sessionId,
            string purpose,
            string model,
            ChatMessageContent? result,
            CancellationToken ct = default)
        {
            var (prompt, completion) = TryExtractTokens(result);
            return RecordAsync(sellerId, sessionId, purpose, model, prompt, completion, ct);
        }

        public static decimal EstimateCost(int promptTokens, int completionTokens)
        {
            if (promptTokens <= 0 && completionTokens <= 0) return 0m;
            return Math.Round(
                promptTokens / 1_000_000m * PromptUsdPer1M
                + completionTokens / 1_000_000m * CompletionUsdPer1M,
                6,
                MidpointRounding.AwayFromZero);
        }

        public static (int Prompt, int Completion) TryExtractTokens(ChatMessageContent? result)
        {
            if (result?.Metadata == null || result.Metadata.Count == 0)
                return (0, 0);

            foreach (var key in new[] { "Usage", "usage", "TokenUsage", "token_usage" })
            {
                if (!result.Metadata.TryGetValue(key, out var raw) || raw == null)
                    continue;

                var extracted = ExtractFromObject(raw);
                if (extracted.Prompt > 0 || extracted.Completion > 0)
                    return extracted;
            }

            // 扁平字段兜底
            var p = TryReadInt(result.Metadata, "PromptTokens", "prompt_tokens", "InputTokens");
            var c = TryReadInt(result.Metadata, "CompletionTokens", "completion_tokens", "OutputTokens");
            return (p, c);
        }

        private static (int Prompt, int Completion) ExtractFromObject(object raw)
        {
            try
            {
                var t = raw.GetType();
                int prompt = ReadProp(t, raw, "PromptTokens", "InputTokenCount", "InputTokens", "prompt_tokens");
                int completion = ReadProp(t, raw, "CompletionTokens", "OutputTokenCount", "OutputTokens", "completion_tokens");
                if (prompt == 0 && completion == 0)
                {
                    // 某些 SDK 只有 TotalTokens
                    var total = ReadProp(t, raw, "TotalTokens", "TotalTokenCount", "total_tokens");
                    if (total > 0) return (total, 0);
                }
                return (prompt, completion);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static int ReadProp(Type t, object raw, params string[] names)
        {
            foreach (var name in names)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p == null) continue;
                var v = p.GetValue(raw);
                if (v == null) continue;
                if (v is int i) return Math.Max(0, i);
                if (v is long l) return (int)Math.Max(0, l);
                if (int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var parsed))
                    return Math.Max(0, parsed);
            }
            return 0;
        }

        private static int TryReadInt(System.Collections.Generic.IReadOnlyDictionary<string, object?> meta, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (!meta.TryGetValue(k, out var v) || v == null) continue;
                if (v is int i) return Math.Max(0, i);
                if (v is long l) return (int)Math.Max(0, l);
                if (int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out var parsed))
                    return Math.Max(0, parsed);
            }
            return 0;
        }
    }
}
