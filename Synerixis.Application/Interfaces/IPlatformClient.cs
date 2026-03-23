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
        Task SendReplyAsync(string openId, string content, CancellationToken cancellationToken = default);

        /// <summary>
        /// 解析 Webhook 请求，转换为平台消息对象
        /// </summary>
        Task<PlatformMessage> ParseWebhookAsync(HttpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证 Webhook 请求签名
        /// </summary>
        Task<bool> VerifySignatureAsync(HttpRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 平台消息 DTO（统一格式）
    /// </summary>
    public class PlatformMessage
    {
        public string Platform { get; set; } = string.Empty;     // 'TAOBAO', 'DOUYIN'
        public string OpenId { get; set; } = string.Empty;       // 买家 open_id
        public string? CustomerId { get; set; }                  // 买家用户ID（可选）
        public string? CustomerName { get; set; }                // 买家昵称
        public string Content { get; set; } = string.Empty;      // 消息内容
        public string MessageType { get; set; } = "text";        // 消息类型：text/image/etc
        public string? MsgId { get; set; }                       // 平台消息ID（去重用）
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
