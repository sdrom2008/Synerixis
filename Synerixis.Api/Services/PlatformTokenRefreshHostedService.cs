using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Synerixis.Api.Services
{
    /// <summary>
    /// 后台扫描即将过期 / 已过期的 PlatformConnection 并调用 RefreshTokenAsync。
    /// 失败只记日志，不抛崩进程。
    /// </summary>
    public class PlatformTokenRefreshHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PlatformTokenRefreshHostedService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(45);
        /// <summary>提前刷新窗口：过期前 1 小时内视为需要刷新。</summary>
        private static readonly TimeSpan RefreshAhead = TimeSpan.FromHours(1);
        /// <summary>无 TokenExpiresAt 时，距上次更新超过该时长则刷新。</summary>
        private static readonly TimeSpan StaleWithoutExpiry = TimeSpan.FromHours(3);

        public PlatformTokenRefreshHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<PlatformTokenRefreshHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 启动后稍等，避免与 EnsureCreated 抢连
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshDueConnectionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TokenRefresh] 扫描周期异常（已吞并继续）");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RefreshDueConnectionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPlatformConnectionRepository>();
            var platformService = scope.ServiceProvider.GetRequiredService<IMerchantPlatformService>();

            var connections = (await repo.GetActiveWithRefreshTokenAsync()).ToList();
            if (connections.Count == 0) return;

            var now = DateTime.UtcNow;
            var due = connections.Where(c =>
            {
                if (c.TokenExpiresAt.HasValue)
                    return c.TokenExpiresAt.Value <= now.Add(RefreshAhead);
                var anchor = c.UpdatedAt ?? c.CreatedAt;
                return anchor <= now.Subtract(StaleWithoutExpiry);
            }).ToList();

            if (due.Count == 0)
            {
                _logger.LogDebug("[TokenRefresh] 无待刷新连接（共 {Total} 条活跃）", connections.Count);
                return;
            }

            _logger.LogInformation("[TokenRefresh] 开始刷新 {Count}/{Total} 条连接", due.Count, connections.Count);

            foreach (var conn in due)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var ok = await platformService.RefreshTokenAsync(conn.Platform, conn);
                    if (ok)
                        _logger.LogInformation(
                            "[TokenRefresh] 成功 Platform={Platform} ConnectionId={Id} ShopId={ShopId}",
                            conn.Platform, conn.Id, conn.ShopId);
                    else
                        _logger.LogWarning(
                            "[TokenRefresh] 失败 Platform={Platform} ConnectionId={Id}",
                            conn.Platform, conn.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[TokenRefresh] 异常 Platform={Platform} ConnectionId={Id}",
                        conn.Platform, conn.Id);
                }
            }
        }
    }
}
