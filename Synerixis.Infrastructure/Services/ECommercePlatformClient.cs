using System;
using System.Net.Http;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces.Infrastructure;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// 电商平台客户端占位实现。承运商轨迹 API 未接通时物流查询返回 null，
    /// 由 LogisticsAgent 回退到「已解析运单号 + 订单状态」，禁止编造模拟运单。
    /// </summary>
    public class ECommercePlatformClient : IECommercePlatformClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ECommercePlatformClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<OrderDetailsDto> GetOrderDetailsAsync(string platform, string orderId)
        {
            Console.WriteLine($"[Infrastructure] GetOrderDetailsAsync platform={platform} order={orderId} (stub)");
            await Task.Delay(50);
            // 订单详情请走 IPlatformClientRouter / OrderAgent 回源；此处不再返回假数据
            return null!;
        }

        public async Task<LogisticsDetailsDto> GetLogisticsDetailsAsync(string platform, string trackingId)
        {
            Console.WriteLine($"[Infrastructure] GetLogisticsDetailsAsync platform={platform} tracking={trackingId} (no carrier API)");
            await Task.Delay(50);
            // 无真实承运商 API：一律 null，避免 SIMULATED_TRACKING_67890 类假命中
            return null!;
        }
    }
}
