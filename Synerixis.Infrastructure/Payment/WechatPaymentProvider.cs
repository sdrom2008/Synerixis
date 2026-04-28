using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Payment
{
    public class WechatPaymentProvider : IPaymentProvider
    {
        public string Channel => "wechat";

        private readonly WeChatPayV3Client _client;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public WechatPaymentProvider(WeChatPayV3Client client, AppDbContext db, IConfiguration config)
        {
            _client = client;
            _db = db;
            _config = config;
        }

        public async Task<PaymentCreateResult> CreateOrderAsync(PaymentCreateRequest request, Guid sellerId)
        {
            // 金额转分
            int amountInFen = (int)(request.Amount * 100);

            //生成订单号（如果前端没有传）
            if (string.IsNullOrEmpty(request.OutTradeNo))
            {
                // 推荐格式：前缀 + Guid（32位唯一）
                request.OutTradeNo = $"SUB{Guid.NewGuid():N}".Substring(0, 32);
                // 或者更短：SUB + 时间戳 + 随机6位
                // request.OutTradeNo = $"SUB{Guid.NewGuid():N}".Substring(0, 32); // 截取前32位
            }

            //订单传过去 weixin订单产生
             var payParams = await _client.CreateJsApiOrderAsync(
                request.OutTradeNo,
                amountInFen,
                request.Description ?? "Synerixis Subscription",
                request.OpenId ?? "",
                request.NotifyUrl ?? _config["WeChatPay:NotifyUrl"] ?? ""
            );

            // 保存订单（从 Controller 迁移过来）
            var order = new PayOrder
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId,
                OutTradeNo = request.OutTradeNo,
                Amount = request.Amount,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                Channel = "wechat"
            };
            _db.PayOrders.Add(order);

            //await _db.SaveChangesAsync();
            try
            {
                await _db.SaveChangesAsync();
                Console.WriteLine("数据库保存成功");
            }
            catch (DbUpdateException dbEx)
            {
                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                Console.WriteLine("DbUpdateException 失败: " + innerMsg);
                if (dbEx.InnerException != null)
                    Console.WriteLine("Inner Exception: " + dbEx.InnerException.ToString());

                throw;  // 继续抛出，让控制器捕获
            }
            catch (Exception ex)
            {
                Console.WriteLine("其他异常: " + ex.ToString());
                throw;
            }

            return new PaymentCreateResult
            {
                Success = true,
                PayParams = payParams.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
            };
        }

        public async Task<PaymentNotifyResult> HandleNotifyAsync(HttpRequest httpRequest)
        {
            // 调用 Client 的 HandleCallbackAsync 或直接复用解密逻辑
            var response = await _client.HandleCallbackAsync();  // 如果 Client 有这个方法

            // 假设返回 JSON 字符串，解析
            if (response.Contains("\"SUCCESS\""))
            {
                // 解密后逻辑已在 Client 里处理了订单更新
                return new PaymentNotifyResult { Success = true };
            }

            return new PaymentNotifyResult { Success = false, Message = "验证失败" };
        }
    }
}
