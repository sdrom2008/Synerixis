using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 平台连接信息（跨境电商平台）
    /// Phase 1: Shopee + TikTok Shop
    /// Phase 2（可选）：Lazada、Amazon、AliExpress
    /// </summary>
    public class PlatformConnection : AggregateRoot<Guid>
    {
        public Guid SellerId { get; private set; }          // 关联 sellers.Id
        public string Platform { get; private set; } = string.Empty;   // 'SHOPEE', 'TIKTOK', 'LAZADA'
        public string AppKey { get; private set; } = string.Empty;    // 应用公钥
        public string AccessToken { get; private set; } = string.Empty; // 访问令牌（加密存储）
        public string? RefreshToken { get; private set; }              // 刷新令牌
        public string OpenId { get; private set; } = string.Empty;      // 店铺在平台的 open_id
        public string? ShopId { get; private set; }                   // 店铺ID（平台侧）
        public string? Nickname { get; private set; }                  // 店铺名称
        public string? AvatarUrl { get; private set; }                 // 店铺头像
        public bool IsActive { get; private set; } = true;             // 是否启用
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }

        // 导航属性
        public Seller? Seller { get; private set; }

        private PlatformConnection() { }

        public static PlatformConnection Create(
            Guid sellerId,
            string platform,
            string appKey,
            string accessToken,
            string openId,
            string? shopId = null,
            string? nickname = null,
            string? avatarUrl = null)
        {
            return new PlatformConnection
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId,
                Platform = platform,
                AppKey = appKey,
                AccessToken = accessToken,
                OpenId = openId,
                ShopId = shopId,
                Nickname = nickname,
                AvatarUrl = avatarUrl,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void UpdateToken(string accessToken, string? refreshToken = null)
        {
            AccessToken = accessToken;
            if (refreshToken != null) RefreshToken = refreshToken;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// OAuth 重新绑定时刷新店铺画像，并确保 ShopId/OpenId 与平台侧一致。
        /// </summary>
        public void UpsertFromOAuth(
            string accessToken,
            string? refreshToken,
            string? shopId,
            string? openId,
            string? nickname,
            string? avatarUrl)
        {
            AccessToken = accessToken;
            if (refreshToken != null) RefreshToken = refreshToken;
            if (!string.IsNullOrEmpty(shopId)) ShopId = shopId;
            if (!string.IsNullOrEmpty(openId)) OpenId = openId;
            if (!string.IsNullOrEmpty(nickname)) Nickname = nickname;
            if (!string.IsNullOrEmpty(avatarUrl)) AvatarUrl = avatarUrl;
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateProfile(string? nickname = null, string? avatarUrl = null)
        {
            if (!string.IsNullOrEmpty(nickname)) Nickname = nickname;
            if (!string.IsNullOrEmpty(avatarUrl)) AvatarUrl = avatarUrl;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
