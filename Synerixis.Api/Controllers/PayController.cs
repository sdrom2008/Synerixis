using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Synerixis.Api.Controllers
{
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

        // 统一创建支付接口（支持微信/支付宝）
        [HttpPost("create")]
        [IgnoreAntiforgeryToken]  // 加这一行，忽略防伪
        public async Task<IActionResult> Create([FromBody] PaymentCreateRequest request)
        {
            if (string.IsNullOrEmpty(request.Channel))
                return BadRequest("缺少支付渠道 (wechat/alipay)");

            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized("无效身份");

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId);
            if (seller == null)
                return NotFound("商户不存在");

            //获取openid 微信用的
            // 微信支付：必须有 OpenId，从数据库读
            if (request.Channel == "wechat")
            {
                if (string.IsNullOrEmpty(seller.OpenId))
                    return BadRequest("请先绑定微信账号");

                // 直接写到 request 里（或传给 provider 的另一个参数，随你喜欢）
                request.OpenId = seller.OpenId;
            }

            var provider = _factory.GetProvider(request.Channel);
            if (provider == null)
                return BadRequest($"不支持渠道: {request.Channel}");

            // 微信需要 OpenId
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

                // 步骤1: 让 provider 处理回调，获取结果
                var notifyResult = await provider.HandleNotifyAsync(Request);

                if (!notifyResult.Success)
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[FAIL]]></return_code><return_msg><![CDATA[验证失败]]></return_msg></xml>" : "fail");
                }

                // 步骤2: 查找订单（OutTradeNo 必须唯一）
                var order = await _db.PayOrders.FirstOrDefaultAsync(o => o.OutTradeNo == notifyResult.OutTradeNo);
                if (order == null)
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>" : "success");  // 微信要求返回 SUCCESS
                }

                if (order.Status == "paid")
                {
                    return Content(channel == "wechat" ? "<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>" : "success");
                }

                // 步骤3: 更新订单状态
                order.Status = "paid";
                order.TransactionId = notifyResult.TransactionId;
                order.PaidAt = DateTime.UtcNow;
                order.Channel = channel;  // 记录渠道（可选）

                // 步骤4: 升级订阅（统一处理）
                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == order.SellerId);
                if (seller != null)
                {
                    seller.ApplySubscription(order.Amount);
                }

                await _db.SaveChangesAsync();

                // 步骤5: 返回微信/支付宝要求的响应格式
                if (channel == "wechat")
                {
                    return Content("<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>", "text/xml");
                }
                else
                {
                    return Content("success");  // 支付宝回调返回 success
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

            // 可选：校验当前用户是否是订单拥有者
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (sellerIdStr != order.SellerId.ToString())
                return Unauthorized("无权查看此订单");

            return Ok(new
            {
                status = order.Status,
                paidAt = order.PaidAt,
                amount = order.Amount,
                transactionId = order.TransactionId
            });
        }
    }

 
}