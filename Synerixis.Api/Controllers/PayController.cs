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
                return BadRequest("缺少支付渠道 (wechat/alipay)");

            var (seller, err) = await ResolvePayingSellerAsync();
            if (err != null) return err;
            var sellerId = seller!.Id;

            if (request.Channel == "wechat")
            {
                if (string.IsNullOrEmpty(seller.OpenId))
                    return BadRequest("请先绑定微信账号");
                request.OpenId = seller.OpenId;
            }

            var provider = _factory.GetProvider(request.Channel);
            if (provider == null)
                return BadRequest($"不支持渠道: {request.Channel}");

            if (request.Channel == "wechat" && string.IsNullOrEmpty(seller.OpenId))
                return BadRequest("请先绑定微信");

            var result = await provider.CreateOrderAsync(request, sellerId);

            if (!result.Success)
                return BadRequest(result.Message);

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
