using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Clients;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Services
{
    /// <summary>
    /// 商户平台绑定服务实现
    /// </summary>
    public class MerchantPlatformService : IMerchantPlatformService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MerchantPlatformService> _logger;
        private readonly PlatformClientRouter _clientRouter;
        private readonly IPlatformConnectionRepository _connectionRepository;
        private readonly AppDbContext _db;

        public MerchantPlatformService(
            IConfiguration config,
            ILogger<MerchantPlatformService> logger,
            PlatformClientRouter clientRouter,
            IPlatformConnectionRepository connectionRepository,
            AppDbContext db)
        {
            _config = config;
            _logger = logger;
            _clientRouter = clientRouter;
            _connectionRepository = connectionRepository;
            _db = db;
        }

        /// <summary>
        /// 获取可绑定的平台列表
        /// </summary>
        public async Task<IEnumerable<PlatformInfo>> GetAvailablePlatformsAsync()
        {
            var platforms = new List<PlatformInfo>
            {
                new PlatformInfo
                {
                    Platform = "SHOPEE",
                    Name = "Shopee (虾皮)",
                    Description = "东南亚及台湾地区领先的电商平台",
                    AuthorizationUrlTemplate = "https://partner.shopeemobile.com/mobile/openplatform/seller",
                    TokenRefreshInterval = TimeSpan.FromDays(30)
                },
                new PlatformInfo
                {
                    Platform = "TIKTOK",
                    Name = "TikTok Shop",
                    Description = "全球短视频电商平台",
                    AuthorizationUrlTemplate = "https://www.tiktok.com/business/openapi/auth",
                    TokenRefreshInterval = TimeSpan.FromDays(30)
                }
            };
            return platforms;
        }

        /// <summary>
        /// 获取指定平台的 OAuth 授权 URL
        /// </summary>
        public async Task<string> GetAuthorizationUrlAsync(string platform, string state)
        {
            var client = _clientRouter.GetClient(platform);
            return await client.GetAuthorizationUrlAsync(state);
        }

        /// <summary>
        /// 通过授权码获取平台信息并绑定店铺
        /// </summary>
        public async Task<PlatformConnectionResult> BindShopAsync(string platform, string authorizationCode, Guid sellerId)
        {
            try
            {
                var client = _clientRouter.GetClient(platform);
                var (accessToken, refreshToken) = await client.GetAccessTokenAsync(authorizationCode, state: "bind");
                
                var (shopId, nickname, avatarUrl) = await client.GetShopInfoAsync(accessToken);
                
                var connection = PlatformConnection.Create(
                    sellerId: sellerId,
                    platform: platform,
                    appKey: platform == "SHOPEE" ? _config["Shopee:AppKey"] : _config["TikTok:ClientKey"],
                    accessToken: accessToken,
                    openId: shopId,
                    shopId: shopId,
                    nickname: nickname,
                    avatarUrl: avatarUrl
                );

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    connection.UpdateToken(accessToken, refreshToken);
                }

                await _connectionRepository.AddAsync(connection);
                await _db.SaveChangesAsync();

                return new PlatformConnectionResult(
                    connection.Id,
                    platform,
                    success: true,
                    shopId: shopId,
                    nickname: nickname
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "绑定店铺失败: {Platform}", platform);
                return new PlatformConnectionResult(
                    Guid.Empty,
                    platform,
                    success: false,
                    error: ex.Message
                );
            }
        }

        /// <summary>
        /// 刷新平台 access token（处理 token 过期）
        /// </summary>
        public async Task<bool> RefreshTokenAsync(string platform, PlatformConnection connection)
        {
            if (string.IsNullOrEmpty(connection.RefreshToken))
                return false;

            try
            {
                var client = _clientRouter.GetClient(platform);
                var (accessToken, _) = await client.GetAccessTokenAsync(connection.RefreshToken, "refresh");
                
                connection.UpdateToken(accessToken, connection.RefreshToken);
                await _connectionRepository.UpdateAsync(connection);
                await _db.SaveChangesAsync();
                
                _logger.LogInformation("Token 刷新成功: {Platform}", platform);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token 刷新失败: {Platform}", platform);
                return false;
            }
        }

        /// <summary>
        /// 解绑平台店铺
        /// </summary>
     public async Task<bool> UnbindShopAsync(string platform, Guid shopId)
        {
            var connection = await _connectionRepository.GetByIdAsync(shopId);
            if (connection == null || !connection.IsActive)
                return false;

            connection.SetActive(isActive: false);
            await _connectionRepository.UpdateAsync(connection);
            await _db.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// 批量验证并绑定多个店铺
        /// </summary>
        public async Task<BatchBindResult> BatchBindShopsAsync(string platform, IEnumerable<string> shopOpenIds)
        {
            throw new NotImplementedException("批量绑定功能暂不实现");
        }
    }
}