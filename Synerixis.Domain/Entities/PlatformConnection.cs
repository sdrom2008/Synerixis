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
        /// <summary>AccessToken 预计过期时间（UTC）；用于后台刷新扫描。</summary>
        public DateTime? TokenExpiresAt { get; private set; }
        /// <summary>最近一次 Token 刷新时间（UTC）。</summary>
        public DateTime? LastRefreshAt { get; private set; }
        /// <summary>最近一次刷新失败原因（成功时清空）；用于引导重新 OAuth。</summary>
        public string? LastRefreshError { get; private set; }
        /// <summary>平台站点/区域（Shopee：SG/TW/VN/…；缺省按配置 Shopee:Region 或 SG）。</summary>
        public string? Region { get; private set; }

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
            string? avatarUrl = null,
            string? region = null)
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
                Region = string.IsNullOrWhiteSpace(region) ? null : region.Trim().ToUpperInvariant(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                TokenExpiresAt = DateTime.UtcNow.AddHours(4)
            };
        }

        public void UpdateToken(string accessToken, string? refreshToken = null, DateTime? tokenExpiresAt = null)
        {
            AccessToken = accessToken;
            if (refreshToken != null) RefreshToken = refreshToken;
            UpdatedAt = DateTime.UtcNow;
            TokenExpiresAt = tokenExpiresAt ?? DateTime.UtcNow.AddHours(4);
            LastRefreshAt = DateTime.UtcNow;
            LastRefreshError = null;
        }

        /// <summary>记录刷新失败（保留旧 Token，供 UI 引导重新绑定）。</summary>
        public void RecordRefreshFailure(string? error)
        {
            LastRefreshAt = DateTime.UtcNow;
            LastRefreshError = string.IsNullOrWhiteSpace(error)
                ? "TOKEN_REFRESH_FAILED"
                : (error.Length > 512 ? error.Substring(0, 512) : error);
            UpdatedAt = DateTime.UtcNow;
        }

        public void ClearRefreshError()
        {
            LastRefreshError = null;
            LastRefreshAt = DateTime.UtcNow;
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
            TokenExpiresAt = DateTime.UtcNow.AddHours(4);
            LastRefreshAt = DateTime.UtcNow;
            LastRefreshError = null;
        }

        public void SetRegion(string? region)
        {
            if (!string.IsNullOrWhiteSpace(region))
                Region = region.Trim().ToUpperInvariant();
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
