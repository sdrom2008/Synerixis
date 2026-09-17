using Microsoft.AspNetCore.Http;
using Synerixis.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Application.Interfaces
{
    public interface IPaymentProvider
    {
        string Channel { get; }  // "wechat", "alipay"

        /// <summary>商户号/证书/密钥等已配置，可发起真实下单；否则勿调用网关。</summary>
        bool IsConfigured { get; }

        Task<PaymentCreateResult> CreateOrderAsync(PaymentCreateRequest request, Guid sellerId);

        Task<PaymentNotifyResult> HandleNotifyAsync(HttpRequest httpRequest);
    }
}
