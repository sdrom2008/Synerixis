using System;
using System.Collections.Generic;

namespace Synerixis.Application.DTOs
{
    /// <summary>
    /// 平台物流轨迹（仅来自 Open API；无权限/失败时 checkpoints 为空，禁止编造）。
    /// </summary>
    public class PlatformTrackingInfoDto
    {
        public string? TrackingNumber { get; set; }
        public string? OrderStatus { get; set; }
        public string? LogisticsStatus { get; set; }
        public List<TrackingCheckpointDto> Checkpoints { get; set; } = new();
        /// <summary>true 表示 API 返回了至少一条真实 checkpoint</summary>
        public bool HasTrajectory => Checkpoints.Count > 0;
        /// <summary>降级原因：api_failed / no_permission / no_tracking / unsupported 等</summary>
        public string? Warning { get; set; }
        public string? Message { get; set; }
    }

    public class TrackingCheckpointDto
    {
        public DateTime? Time { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
    }
}
