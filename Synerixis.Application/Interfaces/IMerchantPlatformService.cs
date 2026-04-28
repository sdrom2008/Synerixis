using Synerixis.Application.DTOs;
using Synerixis.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 商户平台服务接口 - 用于跨境电商平台绑定
    /// </summary>
    public interface IMerchantPlatformService
    {
        /// <summary>
        /// 获取可绑定的平台列表
        /// </summary>
        Task<IEnumerable<PlatformInfo>> GetAvailablePlatformsAsync();

        /// <summary>
        /// 获取指定平台的 OAuth 授权 URL
        /// </summary>
        Task<string> GetAuthorizationUrlAsync(string platform, string state);

        /// <summary>
        /// 通过授权码获取平台信息并绑定店铺
        /// </summary>
        Task<PlatformConnectionResult> BindShopAsync(string platform, string authorizationCode, Guid sellerId);

        /// <summary>
        /// 刷新平台 access token（处理 token 过期）
        /// </summary>
        Task<bool> RefreshTokenAsync(string platform, PlatformConnection connection);

        /// <summary>
        /// 解绑平台店铺
        /// </summary>
        Task<bool> UnbindShopAsync(string platform, Guid shopId);

        /// <summary>
        /// 批量验证并绑定多个店铺
        /// </summary>
        Task<BatchBindResult> BatchBindShopsAsync(string platform, IEnumerable<string> shopOpenIds);
    }

    /// <summary>
    /// 平台信息 DTO
    /// </summary>
    public class PlatformInfo
    {
        public string Platform { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string AuthorizationUrlTemplate { get; set; } = null!;
        public TimeSpan TokenRefreshInterval { get; set; }
    }

    /// <summary>
    /// 平台绑定结果
    /// </summary>
    public class PlatformConnectionResult
    {
        public Guid ConnectionId { get; set; }
        public string Platform { get; set; } = null!;
        public bool Success { get; set; }
        public string? ShopId { get; set; }
        public string? OpenId { get; set; }
        public string? Nickname { get; set; }
        public string? Error { get; set; }

        public PlatformConnectionResult() { }

        public PlatformConnectionResult(Guid id, string platform, bool success, string? shopId = null, string? nickname = null, string? error = null)
        {
            ConnectionId = id;
            Platform = platform;
            Success = success;
            ShopId = shopId;
            Nickname = nickname;
            Error = error;
        }
    }

    /// <summary>
    /// 批量绑定结果
    /// </summary>
    public class BatchBindResult
    {
        public bool Success { get; set; }
        public int TotalCount { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
        public List<BindResultItem> Items { get; set; } = new List<BindResultItem>();
        public string? Summary { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        public class BindResultItem
        {
            public string ShopOpenId { get; set; } = null!;
            public string Platform { get; set; } = null!;
            public bool Success { get; set; }
            public Guid? ConnectionId { get; set; }
            public string? ShopId { get; set; }
            public string? Error { get; set; }
        }
    }
}
