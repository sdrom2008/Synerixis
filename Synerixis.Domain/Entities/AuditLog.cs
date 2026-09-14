using System;
using System.ComponentModel.DataAnnotations;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 轻量审计日志：关键写操作可追溯（绑店、团队、草稿审发、AI 设置、Admin 敏感操作等）。
    /// </summary>
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? ActorId { get; set; }

        /// <summary>Seller | Agent | Supervisor | Admin | System</summary>
        [MaxLength(32)]
        public string ActorType { get; set; } = "System";

        /// <summary>如 connection.bind / draft.approve / team.create</summary>
        [MaxLength(64)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? ResourceType { get; set; }

        [MaxLength(64)]
        public string? ResourceId { get; set; }

        /// <summary>JSON 明细（勿存密钥/Token）</summary>
        public string? DetailJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>商家 Seller.Id（= ChatSession.ShopId）；平台级 Admin 操作可为空</summary>
        public Guid? ShopId { get; set; }
    }

    public static class AuditActions
    {
        public const string ConnectionBind = "connection.bind";
        public const string ConnectionUnbind = "connection.unbind";
        public const string ConnectionRefresh = "connection.refresh";
        public const string TeamCreate = "team.create";
        public const string TeamUpdate = "team.update";
        public const string TeamDisable = "team.disable";
        public const string TeamResetPassword = "team.reset_password";
        public const string DraftApprove = "draft.approve";
        public const string DraftReject = "draft.reject";
        public const string SessionHandoff = "session.handoff";
        public const string AiSettingsUpdate = "ai_settings.update";
        public const string AdminSensitive = "admin.sensitive";
    }
}
