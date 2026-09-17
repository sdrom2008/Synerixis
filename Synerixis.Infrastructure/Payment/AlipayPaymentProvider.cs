using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Synerixis.Infrastructure.Payment
{
    public class AlipayPaymentProvider : IPaymentProvider
    {
        public string Channel => "alipay";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_appId) && !string.IsNullOrWhiteSpace(_privateKey);

        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly string _appId;
        private readonly string _privateKey;
        private readonly string _alipayPublicKey;
        private readonly string _notifyUrl;
        private readonly string _returnUrl;

        public AlipayPaymentProvider(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
            _appId = config["Alipay:AppId"];
            _privateKey = config["Alipay:PrivateKey"];
            _alipayPublicKey = config["Alipay:AlipayPublicKey"];
            _notifyUrl = config["Alipay:NotifyUrl"];
            _returnUrl = config["Alipay:ReturnUrl"];
        }

        public async Task<PaymentCreateResult> CreateOrderAsync(PaymentCreateRequest request, Guid sellerId)
        {
            if (!IsConfigured)
            {
                return new PaymentCreateResult
                {
                    Success = false,
                    Message = "未配置支付证书"
                };
            }

            var parameters = new Dictionary<string, string>
            {
                ["app_id"] = _appId,
                ["method"] = "alipay.trade.page.pay",  // 网页支付（手机/电脑通用）
                ["format"] = "JSON",
                ["charset"] = "utf-8",
                ["sign_type"] = "RSA2",
                ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["version"] = "1.0",
                ["notify_url"] = request.NotifyUrl ?? _notifyUrl,
                ["return_url"] = request.ReturnUrl ?? _returnUrl,
                ["biz_content"] = System.Text.Json.JsonSerializer.Serialize(new
                {
                    out_trade_no = request.OutTradeNo,
                    product_code = "FAST_INSTANT_TRADE_PAY",
                    total_amount = request.Amount.ToString("F2"),
                    subject = request.Description ?? "Synerixis AI 月度订阅",
                    // 可加：timeout_express = "30m"
                })
            };

            // 生成签名内容
            var signContent = AlipaySignatureHelper.GetSignContent(parameters);
            var sign = AlipaySignatureHelper.Sign(signContent, _privateKey);

            parameters["sign"] = sign;

            // 构造 form 表单提交（前端直接跳转或 post）
            var paymentUrl = "https://openapi.alipay.com/gateway.do?" + BuildQuery(parameters);


            if (string.IsNullOrEmpty(request.OutTradeNo))
            {
                // 推荐格式：前缀 + Guid（32位唯一）
                request.OutTradeNo = $"ZSUB{Guid.NewGuid():N}".Substring(0, 32); ;
                // 或者更短：SUB + 时间戳 + 随机6位
                // request.OutTradeNo = $"SUB{Guid.NewGuid():N}".Substring(0, 32); // 截取前32位
            }

            // 保存订单（和微信一致）
            var order = new PayOrder
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId, ///* 从上下文取 sellerId */,
                OutTradeNo = request.OutTradeNo,
                Amount = request.Amount,
                Status = "pending",
                Channel = "alipay",
                CreatedAt = DateTime.UtcNow
            };
            _db.PayOrders.Add(order);
            await _db.SaveChangesAsync();

            return new PaymentCreateResult
            {
                Success = true,
                PaymentUrl = paymentUrl  // 前端直接 location.href = paymentUrl
            };
        }

        public async Task<PaymentNotifyResult> HandleNotifyAsync(HttpRequest httpRequest)
        {
            var form = await httpRequest.ReadFormAsync();
            var parameters = new Dictionary<string, string>();

            foreach (var key in form.Keys)
            {
                parameters[key] = form[key];
            }

            var sign = parameters.GetValueOrDefault("sign");
            var signContent = AlipaySignatureHelper.GetSignContent(parameters);

            if (AlipaySignatureHelper.Verify(signContent, sign, _alipayPublicKey))
            {
                var outTradeNo = parameters["out_trade_no"];
                var tradeNo = parameters["trade_no"];
                var tradeStatus = parameters["trade_status"];

                if (tradeStatus == "TRADE_SUCCESS" || tradeStatus == "TRADE_FINISHED")
                {
                    return new PaymentNotifyResult
                    {
                        Success = true,
                        OutTradeNo = outTradeNo,
                        TransactionId = tradeNo
                    };
                }
            }

            return new PaymentNotifyResult { Success = false, Message = "签名验证失败" };
        }

        private string BuildQuery(Dictionary<string, string> dict)
        {
            var sb = new StringBuilder();
            foreach (var kv in dict)
            {
                sb.Append(HttpUtility.UrlEncode(kv.Key)).Append("=")
                  .Append(HttpUtility.UrlEncode(kv.Value)).Append("&");
            }
            return sb.ToString().TrimEnd('&');
        }
    }
}