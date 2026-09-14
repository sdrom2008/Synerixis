using Synerixis.Domain.Enums;

namespace Synerixis.Application.DTOs
{
    /// <summary>意图分类结果（含粗置信度，供自动 handoff 阈值判断）</summary>
    public class IntentClassificationResult
    {
        public ChatIntent Intent { get; set; } = ChatIntent.Unknown;
        /// <summary>0~1；Unknown/失败偏低，明确类别偏高</summary>
        public double Confidence { get; set; }
        public string? RawLabel { get; set; }
    }
}
