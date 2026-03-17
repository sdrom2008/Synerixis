using System;
using System.Net.Http;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces.Infrastructure;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// A client to interact with various e-commerce platforms like Taobao and Douyin.
    /// This is a concrete implementation of the IECommercePlatformClient interface.
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
            // In a real implementation, we would use the HttpClient to call the platform's API.
            // For now, we return mock data to allow for end-to-end testing.

            Console.WriteLine($"[Infrastructure] Simulating API call to {platform} for order: {orderId}");

            // TODO: Replace this mock implementation with actual API calls.
            await Task.Delay(150); // Simulate network latency

            if (orderId.Contains("12345"))
            {
                return new OrderDetailsDto(orderId, "已发货", 199.99m);
            }
            return null; // Simulate order not found
        }

        public async Task<LogisticsDetailsDto> GetLogisticsDetailsAsync(string platform, string trackingId)
        {
            Console.WriteLine($"[Infrastructure] Simulating API call to {platform} for tracking: {trackingId}");

            // TODO: Replace this mock implementation with actual API calls.
            await Task.Delay(150); // Simulate network latency

            if (trackingId.Contains("67890"))
            {
                return new LogisticsDetailsDto(trackingId, "运输中", "包裹已到达 [深圳福田] 中转中心");
            }
            return null; // Simulate tracking not found
        }
    }
}
