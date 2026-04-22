using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 平台客户端接口（淘宝、抖音、小红书等）
    /// </summary>
    public interface IPlatformClient
    {
        /// <summary>
        /// 发送消息回复给买家
        /// </summary>
        /// <param name="context">包含会话和平台信息的上下文数据</param>
        /// <param name="content">要发送的消息内容</param>
        Task SendReplyAsync(PlatformMessage context, string content, CancellationToken cancellationToken = default);

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
        Task<string?> GetCustomerOrderAsync(string platform, string customerId, CancellationToken cancellationToken = default);
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
