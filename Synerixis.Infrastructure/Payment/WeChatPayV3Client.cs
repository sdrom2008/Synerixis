using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Synerixis.Infrastructure.Payment
{
    public class WeChatPayV3Client
    {
        private readonly string _mchId;
        private readonly string _appId;
        private readonly string _apiV3Key;
        private readonly X509Certificate2 _merchantCert;
        private readonly string _serialNo;
        private readonly HttpClient _httpClient;


        private readonly HttpContext HttpContext; // 假设通过依赖注入获得
        private readonly AppDbContext _db; // 假设通过依赖注入获得

        public WeChatPayV3Client(string mchId, string appId, string apiV3Key, string certPath, string certPassword, AppDbContext _thisdb)
        {
            _mchId = mchId ?? throw new ArgumentNullException(nameof(mchId));
            _appId = appId ?? throw new ArgumentNullException(nameof(appId));
            _apiV3Key = apiV3Key ?? throw new ArgumentNullException(nameof(apiV3Key));

            _merchantCert = new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.Exportable);
            _serialNo = _merchantCert.GetSerialNumberString().ToUpperInvariant();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Synerixis/1.0");
            _db = _thisdb;

        }

        /// <summary>
        /// 创建 JSAPI 支付订单
        /// </summary>
        public async Task<Dictionary<string, string>> CreateJsApiOrderAsync(
            string outTradeNo,
            int amountInFen,
            string description,
            string openId,
            string notifyUrl,
            string spbillCreateIp = "127.0.0.1")
        {
            var nonceStr = Guid.NewGuid().ToString("N");
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            if (string.IsNullOrEmpty(openId))
                throw new Exception("openid 不能为空");

            var body = new Dictionary<string, object>
            {
                ["appid"] = _appId,
                ["mchid"] = _mchId,
                ["description"] = description,
                ["out_trade_no"] = outTradeNo,  // 如果前端没传，后端生成,
                ["notify_url"] = notifyUrl,
                ["amount"] = new { total = amountInFen, currency = "CNY" },
                ["payer"] = new { openid = openId }
            };

            var jsonBody = JsonSerializer.Serialize(body);
            var signStr = $"POST\n/v3/pay/transactions/jsapi\n{timestamp}\n{nonceStr}\n{jsonBody}\n";

            var signature = SignWithPrivateKey(signStr, _merchantCert.GetRSAPrivateKey());

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{_mchId}\",nonce_str=\"{nonceStr}\",timestamp=\"{timestamp}\",serial_no=\"{_serialNo}\",signature=\"{signature}\"",
                //["Content-Type"] = "application/json",
                ["Accept"] = "application/json"
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mch.weixin.qq.com/v3/pay/transactions/jsapi")
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            foreach (var h in headers)
                request.Headers.Add(h.Key, h.Value);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"统一下单失败: {response.StatusCode} - {responseContent}");
            }

            var result = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
            var prepayId = result["prepay_id"].ToString();

            // 生成前端所需支付参数
            var payParams = BuildJsApiPayParams(prepayId, nonceStr, timestamp);

            return payParams;
        }

        private Dictionary<string, string> BuildJsApiPayParams(string prepayId, string nonceStr, string timestamp)
        {
            var package = $"prepay_id={prepayId}";
            var signStr = $"{_appId}\n{timestamp}\n{nonceStr}\n{package}\n";

            var signature = SignWithPrivateKey(signStr, _merchantCert.GetRSAPrivateKey());

            return new Dictionary<string, string>
            {
                ["appId"] = _appId,
                ["timeStamp"] = timestamp,
                ["nonceStr"] = nonceStr,
                ["package"] = package,
                ["signType"] = "RSA",
                ["paySign"] = signature
            };
        }

        private string SignWithPrivateKey(string signStr, RSA privateKey)
        {
            var data = Encoding.UTF8.GetBytes(signStr);
            var signature = privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        /// <summary>
        /// 处理支付回调（V3）
        /// </summary>
        public async Task<string> HandleCallbackAsync()
        {
            var body = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            // V3 回调是 JSON 格式
            var notify = JsonSerializer.Deserialize<Dictionary<string, object>>(body);

            // 验证签名（简化版，实际需用平台证书验证）
            if (notify == null || !notify.ContainsKey("resource"))
                return "{\"code\":\"FAIL\",\"message\":\"Invalid callback\"}";

            var resource = JsonSerializer.Deserialize<Dictionary<string, object>>(notify["resource"]?.ToString() ?? "{}");
            var cipherText = resource?["ciphertext"]?.ToString() ?? "";
            var nonce = resource?["nonce"]?.ToString() ?? "";
            var associatedData = resource?["associated_data"]?.ToString() ?? "";

            // AES-GCM 解密（V3 标准）
            var decrypted = AesGcmDecrypt(cipherText, _apiV3Key, nonce, associatedData);

            var decryptedObj = JsonSerializer.Deserialize<Dictionary<string, object>>(decrypted) ?? new();
            var outTradeNo = decryptedObj["out_trade_no"]?.ToString() ?? "";
            var transactionId = decryptedObj["transaction_id"]?.ToString() ?? "";

            // 更新订单和商户权益（同前逻辑）
            var order = await _db.PayOrders.FirstOrDefaultAsync(o => o.OutTradeNo == outTradeNo);
            if (order != null && order.Status == "pending")
            {
                order.Status = "paid";
                order.TransactionId = transactionId;
                order.PaidAt = DateTime.UtcNow;

                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == order.SellerId);
                if (seller != null)
                    seller.ApplySubscription(order.Amount / 100m); // 假设金额单位为分

                await _db.SaveChangesAsync();
            }

            return "{\"code\":\"SUCCESS\",\"message\":\"成功\"}";
        }

        private string AesGcmDecrypt(string ciphertext, string key, string nonce, string associatedData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var nonceBytes = Encoding.UTF8.GetBytes(nonce);
            var associatedDataBytes = Encoding.UTF8.GetBytes(associatedData);
            var cipherBytes = Convert.FromBase64String(ciphertext);

            using var aesGcm = new AesGcm(keyBytes);
            var plaintextBytes = new byte[cipherBytes.Length - AesGcm.TagByteSizes.MaxSize];

            aesGcm.Decrypt(nonceBytes, cipherBytes.AsSpan(0, plaintextBytes.Length), cipherBytes.AsSpan(plaintextBytes.Length), plaintextBytes, associatedDataBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}