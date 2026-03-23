using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using System;
using System.Collections.Concurrent;

namespace Synerixis.Infrastructure.Clients
{
    /// <summary>
    /// 平台客户端路由器 - 根据平台类型返回对应客户端实例
    /// </summary>
    public class PlatformClientRouter
    {
        private readonly ILogger<PlatformClientRouter> _logger;
        private readonly ConcurrentDictionary<string, IPlatformClient> _clients;

        public PlatformClientRouter(
            ILogger<PlatformClientRouter> logger,
            TaobaoPlatformClient taobaoClient,
            ShopeePlatformClient shopeeClient
        /* DouyinPlatformClient douyinClient */)
        {
            _logger = logger;
            _clients = new ConcurrentDictionary<string, IPlatformClient>(StringComparer.OrdinalIgnoreCase)
            {
                ["TAOBAO"] = taobaoClient,
                ["SHOPEE"] = shopeeClient
                // ["DOUYIN"] = douyinClient // 待创建
            };
        }

        /// <summary>
        /// 获取指定平台的客户端
        /// </summary>
        public IPlatformClient GetClient(string platform)
        {
            if (!_clients.TryGetValue(platform.ToUpper(), out var client))
            {
                _logger.LogError("[PlatformClientRouter] Unknown platform: {Platform}", platform);
                throw new NotSupportedException($"Platform '{platform}' is not supported");
            }
            return client;
        }

        /// <summary>
        /// 检查平台是否已注册
        /// </summary>
        public bool IsSupported(string platform) => _clients.ContainsKey(platform.ToUpper());
    }
}
