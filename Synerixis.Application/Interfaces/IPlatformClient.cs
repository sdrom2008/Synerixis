using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Synerixis.Application.DTOs;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 平台客户端接口（淘宝、抖音、小红书等）
    /// </summary>
    public interface IPlatformClient
    {
        /// <summary>
        /// 发送消息回复给买家。
        /// 若平台响应含真实 message_id 则返回之；否则返回 null（调用方可用 outbound:{localId} 兜底）。
        /// </summary>
        /// <param name="context">包含会话和平台信息的上下文数据</param>
        /// <param name="content">要发送的消息内容</param>
        /// <returns>平台侧 message_id，或 null</returns>
        Task<string?> SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default);

        /// <summary>
        /// 解析 Webhook 请求，转换为平台消息对象
        /// </summary>
        Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证 Webhook 请求签名
        /// </summary>
        Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 获取客户（买家）的订单信息（非商户订单，而是客户在平台的订单）
        /// </summary>
        Task<string?> GetCustomerOrderAsync(string platform, string customerId, string? platformShopId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 【绑定功能】获取 OAuth 授权 URL
        /// </summary>
        Task<string> GetAuthorizationUrlAsync(string state, string? region = null);

        /// <summary>
        /// 【绑定功能】通过授权码获取 Access Token
        /// </summary>
        Task<(string AccessToken, string RefreshToken)> GetAccessTokenAsync(
            string authorizationCode, string state, string? region = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 【绑定功能】获取店铺信息
        /// </summary>
        Task<(string ShopId, string Nickname, string AvatarUrl)> GetShopInfoAsync(
            string accessToken, string? region = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询物流轨迹。有 order_sn（及可选 tracking）时调平台 API；
        /// 失败/无权限时返回运单号+订单状态且 checkpoints 为空，绝不编造轨迹。
        /// </summary>
        Task<PlatformTrackingInfoDto> GetTrackingInfoAsync(
            string orderSn,
            string? trackingNumber = null,
            string? platformShopId = null,
            string? orderStatusHint = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 平台消息 DTO（统一格式）
    /// </summary>
    public class PlatformMessage
    {
        public string Platform { get; set; } = string.Empty;     // 'TAOBAO', 'DOUYIN', 'SHOPEE'
        public string OpenId { get; set; } = string.Empty;       // 用于标识会话商户侧身份（如 shop_id）
        public string? CustomerId { get; set; }                  // 买家用户ID（可选）
        public string? CustomerName { get; set; }                // 买家昵称
        public string Content { get; set; } = string.Empty;      // 消息内容
        public string MessageType { get; set; } = "text";        // 消息类型：text/image/etc
        public string? MsgId { get; set; }                       // 平台消息ID（去重用）
        public string? ConversationId { get; set; }              // 会话ID（如 Shopee 的 conversation_id），用于回复
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
