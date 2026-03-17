using Synerixis.Domain.Common;
using System;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// Represents an AI-generated marketing copy for a product.
    /// </summary>
    public class MarketingCopy : AggregateRoot<Guid>
    {
        /// <summary>
        /// The ID of the product this copy is for.
        /// </summary>
        public Guid ProductId { get; private set; }

        /// <summary>
        /// The prompt used to generate the copy.
        /// </summary>
        public string Prompt { get; private set; }

        /// <summary>
        /// The AI-generated content.
        /// </summary>
        public string Content { get; private set; }

        /// <summary>
        /// The version or variant of the generated copy.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// The timestamp when the copy was created.
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        // Private constructor for EF Core
        private MarketingCopy() { }

        public MarketingCopy(Guid productId, string prompt, string content, string version)
        {
            Id = Guid.NewGuid();
            ProductId = productId;
            Prompt = prompt;
            Content = content;
            Version = version;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
