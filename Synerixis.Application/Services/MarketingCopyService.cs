using System;
using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Services
{
    /// <summary>
    /// Implements the logic for generating marketing copy.
    /// </summary>
    public class MarketingCopyService : IMarketingCopyService
    {
        private readonly ILlmClient _llmClient;

        public MarketingCopyService(ILlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task<MarketingCopy> GenerateCopyAsync(GenerateCopyDto generateCopyDto)
        {
            var prompt = BuildPrompt(generateCopyDto);

            var usage = new LlmCallContext
            {
                SellerId = Guid.TryParse(generateCopyDto.SellerId, out var sid) ? sid : Guid.Empty,
                Purpose = AiUsagePurposes.Marketing
            };
            var generatedContent = await _llmClient.GenerateTextAsync(
                prompt,
                usage.SellerId == Guid.Empty ? null : usage);

            if (string.IsNullOrWhiteSpace(generatedContent))
            {
                throw new InvalidOperationException("AI failed to generate content.");
            }

            return new MarketingCopy(
                generateCopyDto.ProductId,
                prompt,
                generatedContent,
                "v1.0-AI"
            );
        }

        private string BuildPrompt(GenerateCopyDto dto)
        {
            return $"""
            You are a world-class e-commerce copywriter for the Chinese market.
            Your task is to write a compelling marketing copy for a product.

            Product Name: {dto.ProductName}
            Keywords: {dto.Keywords}
            Desired Tone: {dto.ToneOfVoice}

            Please generate the marketing copy now.
            """;
        }
    }
}
