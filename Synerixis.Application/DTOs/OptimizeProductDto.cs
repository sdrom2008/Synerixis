using System.Collections.Generic;

namespace Synerixis.Application.DTOs
{
    /// <summary>
    /// Request for product optimization via Agent.
    /// </summary>
    public class OptimizeProductRequest
    {
        public string Intent { get; set; } = string.Empty; // "optimize" or similar
        public string? OriginalTitle { get; set; }
        public string? OriginalDescription { get; set; }
        public List<string>? OriginalImageUrls { get; set; }
        public string? Category { get; set; }
        public string? TargetPlatform { get; set; } // e.g., "taobao", "douyin"
    }
}
