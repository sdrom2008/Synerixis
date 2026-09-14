using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Options;
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
        private readonly AppDbContext _db;
        private readonly ILogger<AiUsageRecorder> _logger;
        private readonly AiPricingOptions _pricing;

        public AiUsageRecorder(
            AppDbContext db,
            ILogger<AiUsageRecorder> logger,
            IOptions<AiPricingOptions>? pricing = null)
        {
            _db = db;
            _logger = logger;
            _pricing = pricing?.Value ?? new AiPricingOptions();
        }

        public async Task RecordAsync(
            Guid sellerId,
            Guid? sessionId,
            string purpose,
            string model,
            int promptTokens,
            int completionTokens,
            bool isEstimated = false,
            CancellationToken ct = default)
        {
            if (sellerId == Guid.Empty) return;

            var prompt = Math.Max(0, promptTokens);
            var completion = Math.Max(0, completionTokens);
            var cost = EstimateCost(prompt, completion, _pricing);

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
                    Purpose = string.IsNullOrWhiteSpace(purpose) ? AiUsagePurposes.Other : purpose.Trim().ToLowerInvariant(),
                    IsEstimated = isEstimated
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
            string? promptText = null,
            string? completionText = null,
            CancellationToken ct = default)
        {
            var (prompt, completion) = TryExtractTokens(result);
            var estimated = false;

            if (prompt <= 0 && completion <= 0)
            {
                var promptSrc = promptText ?? "";
                var completionSrc = completionText ?? result?.Content ?? "";
                prompt = EstimateTokensFromText(promptSrc);
                completion = EstimateTokensFromText(completionSrc);
                estimated = prompt > 0 || completion > 0;
            }

            return RecordAsync(sellerId, sessionId, purpose, model, prompt, completion, estimated, ct);
        }

        /// <summary>粗估：约 4 字符 ≈ 1 token（中英混合够用）。</summary>
        public static int EstimateTokensFromText(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        public static decimal EstimateCost(int promptTokens, int completionTokens, AiPricingOptions? pricing = null)
        {
            pricing ??= new AiPricingOptions();
            if (promptTokens <= 0 && completionTokens <= 0) return 0m;
            var inputRate = pricing.PricePer1kInput;
            var outputRate = pricing.PricePer1kOutput;
            return Math.Round(
                promptTokens / 1000m * inputRate
                + completionTokens / 1000m * outputRate,
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
