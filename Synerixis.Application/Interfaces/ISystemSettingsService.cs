namespace Synerixis.Application.Interfaces
{
    /// <summary>平台运营开关（带短缓存，避免每请求打 DB）</summary>
    public interface ISystemSettingsService
    {
        Task<SystemOpsSettings> GetOpsAsync(CancellationToken ct = default);

        /// <summary>Admin 写入后立刻失效缓存</summary>
        void Invalidate();
    }

    public sealed record SystemOpsSettings(
        bool MaintenanceMode,
        string DefaultOutboundMode,
        bool AllowNewRegistration);
}
