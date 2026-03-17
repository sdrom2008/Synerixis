using System;
using System.Threading.Tasks;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Application.Interfaces.Ai; 

namespace Synerixis.Application.Services
{
    /// <summary>
    /// Implements the logic for generating marketing copy.
    /// </summary>
    public class MarketingCopyService : IMarketingCopyService
    {
        private readonly ILlmClient _llmClient;

        // We use Dependency Injection to get the AI client.
        // The actual implementation (e.g., OpenAiClient) will be in the Infrastructure layer.
        public MarketingCopyService(ILlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task<MarketingCopy> GenerateCopyAsync(GenerateCopyDto generateCopyDto)
        {
            // 1. Construct a high-quality prompt
            var prompt = BuildPrompt(generateCopyDto);

            // 2. Call the AI Large Language Model
            var generatedContent = await _llmClient.GenerateTextAsync(prompt);

            if (string.IsNullOrWhiteSpace(generatedContent))
            {
                // Handle cases where the AI fails to generate content
                throw new InvalidOperationException("AI failed to generate content.");
            }

            // 3. Create a new domain entity with the result
            var marketingCopy = new MarketingCopy(
                generateCopyDto.ProductId,
                prompt,
                generatedContent,
                "v1.0-AI" // A simple versioning
            );

            // In a real implementation, we would also save this to the database
            // via a repository interface, e.g., _marketingCopyRepository.AddAsync(marketingCopy);

            return marketingCopy;
        }

        private string BuildPrompt(GenerateCopyDto dto)
        {
            // This is where the "prompt engineering" happens.
            // A more sophisticated implementation could use templates.
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
