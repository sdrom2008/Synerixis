using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces.Infrastructure
{
    /// <summary>
    /// A unified interface for different e-commerce platform clients.
    /// </summary>
    public interface IECommercePlatformClient
    {
        Task<OrderDetailsDto> GetOrderDetailsAsync(string platform, string orderId);
        Task<LogisticsDetailsDto> GetLogisticsDetailsAsync(string platform, string trackingId);
    }

    /// <summary>
    /// DTO for order details.
    /// </summary>
    public record OrderDetailsDto(string OrderId, string Status, decimal Amount);

    /// <summary>
    /// DTO for logistics details.
    /// </summary>
    public record LogisticsDetailsDto(string TrackingId, string Status, string CurrentLocation);
}
