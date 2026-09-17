using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.DTOs
{
    // 辅助 DTO（放在 Interfaces 或单独文件）
    public class PaymentCreateRequest
    {
        public string Channel { get; set; } = "";   // 支付渠道：wechat / alipay
        public decimal Amount { get; set; }          // 金额（元）
        public string? Description { get; set; }      // 订单描述
        public string? OutTradeNo { get; set; }       // 商户订单号（可选，自生成）
        /// <summary>异步通知地址（可选；缺省用配置 WeChatPay/Alipay NotifyUrl）</summary>
        public string? NotifyUrl { get; set; }
        public string? OpenId { get; set; }           // 微信支付专用：用户 OpenID（JSAPI/H5 需要）
        /// <summary>支付宝同步返回地址（可选；缺省用配置）</summary>
        public string? ReturnUrl { get; set; }
    }
}
