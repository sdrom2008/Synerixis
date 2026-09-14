using Synerixis.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Synerixis.Infrastructure.Clients
{
    /// <summary>
    /// 平台客户端路由器 - 根据平台类型返回对应客户端实例
    /// Phase 1: Shopee + TikTok Shop
    /// Phase 2（可选）：Lazada、Amazon、AliExpress
    /// </summary>
    public class PlatformClientRouter : IPlatformClientRouter
    {
        private readonly ILogger<PlatformClientRouter> _logger;
        private readonly IServiceProvider _serviceProvider;

        private static readonly HashSet<string> _supportedPlatforms = new(StringComparer.OrdinalIgnoreCase)
        {
            "SHOPEE",
            "TIKTOK"
        };

        public PlatformClientRouter(
            ILogger<PlatformClientRouter> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 获取指定平台的客户端（复用当前 Scope，避免 CreateScope 后立即 Dispose 导致客户端失效）
        /// </summary>
        public IPlatformClient GetClient(string platform)
        {
            var normalized = platform.ToUpperInvariant();

            if (!_supportedPlatforms.Contains(normalized))
            {
                _logger.LogError("[PlatformClientRouter] Unknown platform: {Platform}", platform);
                throw new NotSupportedException($"Platform '{platform}' is not supported in Phase 1. Supported: SHOPEE, TIKTOK");
            }

            return normalized switch
            {
                "SHOPEE" => _serviceProvider.GetRequiredService<ShopeePlatformClient>(),
                "TIKTOK" => _serviceProvider.GetRequiredService<TikTokShopPlatformClient>(),
                _ => throw new NotSupportedException($"Platform '{platform}' is not supported")
            };
        }

        /// <summary>
        /// 检查平台是否已注册
        /// </summary>
        public bool IsSupported(string platform) => _supportedPlatforms.Contains(platform.ToUpperInvariant());
    }
}
