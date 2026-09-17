using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Synerixis.Api.Controllers
{
    /// <summary>
    /// 充值/订阅：Seller 与 Supervisor 可发起；Agent 不可。
    /// Supervisor 经 JWT shopId / Agents.ShopId 解析所属商户，勿用 Agent.Id 当 Seller.Id。
    /// </summary>
    [ApiController]
    [Route("api/pay")]
    [Authorize]
    public class PayController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IPaymentProviderFactory _factory;

        public PayController(AppDbContext db, IConfiguration config, IPaymentProviderFactory factory)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        [HttpPost("create")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Create([FromBody] PaymentCreateRequest request)
        {
            if (string.IsNullOrEmpty(request.Channel))
                return BadRequest(new { message = "缺少支付渠道 (wechat/alipay)" });

            var (seller, err) = await ResolvePayingSellerAsync();
            if (err != null) return err;
            var sellerId = seller!.Id;

            IPaymentProvider? provider;
            try
            {
                provider = _factory.GetProvider(request.Channel);
            }
            catch (ArgumentException)
            {
                return BadRequest(new { message = $"不支持渠道: {request.Channel}" });
            }

            if (provider == null)
                return BadRequest(new { message = $"不支持渠道: {request.Channel}" });

            // DEMO / 本地：未配置商户证书时返回明确 4xx，避免证书加载抛出裸 500
            // （先于微信 OpenId 校验，保证无证书场景消息稳定为「未配置支付证书」）
            if (!provider.IsConfigured)
            {
                return BadRequest(new
                {
                    message = "未配置支付证书",
                    code = "PAYMENT_CERT_MISSING",
                    channel = request.Channel,
                    hint = "请配置 WeChatPay / Alipay 商户证书与密钥后再发起真实支付；演示环境可忽略本步。"
                });
            }

            if (request.Channel == "wechat")
            {
                if (string.IsNullOrEmpty(seller.OpenId))
                    return BadRequest(new { message = "请先绑定微信账号" });
                request.OpenId = seller.OpenId;
            }

            PaymentCreateResult result;
            try
            {
                result = await provider.CreateOrderAsync(request, sellerId);
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains("未配置支付证书", StringComparison.Ordinal) ||
                ex.Message.Contains("证书", StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    message = "未配置支付证书",
                    code = "PAYMENT_CERT_MISSING",
                    channel = request.Channel
                });
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return BadRequest(new
                {
                    message = "未配置支付证书",
                    code = "PAYMENT_CERT_MISSING",
                    channel = request.Channel,
                    hint = "商户证书无效或无法加载。"
                });
            }

            if (!result.Success)
            {
                var msg = string.IsNullOrWhiteSpace(result.Message) ? "支付创建失败" : result.Message;
                if (msg.Contains("未配置支付证书", StringComparison.Ordinal) || msg.Contains("证书", StringComparison.Ordinal))
                {
                    return BadRequest(new
                    {
                        message = "未配置支付证书",
                        code = "PAYMENT_CERT_MISSING",
                        channel = request.Channel
                    });
                }
                return BadRequest(new { message = msg });
            }

            return Ok(result);
        }

        [HttpPost("notify/{channel}")]
        [AllowAnonymous]
        public async Task<IActionResult> Notify(string channel)
        {
            try
            {
                var provider = _factory.GetProvider(channel);
                if (provider == null)
                {
                    return Content("fail");
                }

                var notifyResult = await provider.HandleNotifyAsync(Request);

                if (!notifyResult.Success)
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[FAIL]]></return_code><return_msg><![CDATA[验证失败]]></return_msg></xml>" : "fail");
                }

                var order = await _db.PayOrders.FirstOrDefaultAsync(o => o.OutTradeNo == notifyResult.OutTradeNo);
                if (order == null)
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>" : "success");
                }

                if (order.Status == "paid")
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>" : "success");
                }

                order.Status = "paid";
                order.TransactionId = notifyResult.TransactionId;
                order.PaidAt = DateTime.UtcNow;
                order.Channel = channel;

                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == order.SellerId);
                if (seller != null)
                {
                    seller.ApplySubscription(order.Amount);
                }

                await _db.SaveChangesAsync();

                if (channel == "wechat")
                {
                    return Content("<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>", "text/xml");
                }
                else
                {
                    return Content("success");
                }
            }
            catch (Exception)
            {
                return Content(channel == "wechat" ? "<xml><return_code><![CDATA[FAIL]]></return_code><return_msg><![CDATA[服务器错误]]></return_msg></xml>" : "fail");
            }
        }

        [HttpGet("query")]
        [Authorize]
        public async Task<IActionResult> Query([FromQuery] string outTradeNo)
        {
            if (string.IsNullOrEmpty(outTradeNo))
                return BadRequest("订单号不能为空");

            var order = await _db.PayOrders.FirstOrDefaultAsync(o => o.OutTradeNo == outTradeNo);
            if (order == null)
                return NotFound("订单不存在");

            var (seller, err) = await ResolvePayingSellerAsync();
            if (err != null) return err;
            if (seller!.Id != order.SellerId)
                return Unauthorized("无权查看此订单");

            return Ok(new
            {
                status = order.Status,
                paidAt = order.PaidAt,
                amount = order.Amount,
                transactionId = order.TransactionId
            });
        }

        /// <summary>
        /// Seller → UserId；Supervisor → shopId claim 或 Agents.ShopId；Agent/其他 → 403。
        /// support/impersonation token 不可发起支付。
        /// </summary>
        private async Task<(Seller? seller, IActionResult? error)> ResolvePayingSellerAsync()
        {
            var userType = User.FindFirst("userType")?.Value
                ?? User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value
                ?? "";
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("userId")?.Value
                ?? User.FindFirst("uid")?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return (null, Unauthorized("无效身份"));

            var support = User.FindFirst("support")?.Value ?? User.FindFirst("impersonation")?.Value;
            if (string.Equals(support, "true", StringComparison.OrdinalIgnoreCase) || support == "1")
                return (null, StatusCode(403, new { message = "支持会话不可发起支付" }));

            if (string.Equals(userType, "Seller", StringComparison.OrdinalIgnoreCase))
            {
                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == userId);
                if (seller == null)
                    return (null, NotFound("商户不存在"));
                return (seller, null);
            }

            if (string.Equals(userType, "Supervisor", StringComparison.OrdinalIgnoreCase))
            {
                Guid shopId;
                var shopClaim = User.FindFirst("shopId")?.Value;
                if (!Guid.TryParse(shopClaim, out shopId))
                {
                    var agent = await _db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == userId);
                    if (agent == null)
                        return (null, NotFound("坐席不存在"));
                    shopId = agent.ShopId;
                }

                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == shopId);
                if (seller == null)
                    return (null, NotFound("商户不存在"));
                return (seller, null);
            }

            return (null, StatusCode(403, new { message = "仅商家或主管可充值/购买订阅" }));
        }
    }
}
