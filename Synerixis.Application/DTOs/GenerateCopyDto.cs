using System;

namespace Synerixis.Application.DTOs
{
    /// <summary>
    /// Data Transfer Object for generating marketing copy.
    /// </summary>
    public class GenerateCopyDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string Keywords { get; set; }
        public string ToneOfVoice { get; set; } // e.g., "Professional", "Witty", "Sales-oriented"
    }
}
