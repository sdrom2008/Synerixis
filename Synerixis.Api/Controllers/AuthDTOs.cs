namespace Synerixis.Api.Controllers
{
    // 微信登录
    public class WeChatCode
    {
        public string Code { get; set; } = null!;
    }

    public class WeChatResp
    {
        public string OpenId { get; set; } = null!;
        public string? Errmsg { get; set; }
    }

    // 手机登录
    public class PhoneLoginDto
    {
        public string Phone { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string CountryCode { get; set; } = "86";
    }

    // 绑定微信
    public class BindWechatDto
    {
        public string OpenId { get; set; } = null!;
        public string Phone { get; set; } = null!;
    }

    // 发送验证码
    public class SendCodeDto
    {
        public string Phone { get; set; } = null!;
        public string CountryCode { get; set; } = "86";
    }

    // 绑定手机
    public class BindPhoneDto
    {
        public string OpenId { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string CountryCode { get; set; } = "86";
    }

    // 解密手机号
    public class DecryptPhoneDto
    {
        public string Code { get; set; } = null!;
        public string EncryptedData { get; set; } = null!;
        public string Iv { get; set; } = null!;
        public string OpenId { get; set; } = null!;
    }

    // =========================================
    // 绑定 Shopee 店铺 DTOs
    // =========================================
    /// <summary>
    /// Shopee 授权码
    /// </summary>
    public class ShopeeAuthCodeDto
    {
        public string Platform { get; set; } = "SHOPEE";
        public string RedirectUri { get; set; } = null!; // 回调地址
        public string? ShopId { get; set; } // 已绑定店铺 ID（可选，用于更新）
        public string? AppKey { get; set; }
        public string? AppSecret { get; set; }
        public string? AccessToken { get; set; }
        public string? ShopName { get; set; }
    }

    /// <summary>
    /// 刷新 Shopee 店铺令牌
    /// </summary>
    public class RefreshShopeeTokenDto
    {
        public string AppKey { get; set; } = null!;
        public string AppSecret { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }

    /// <summary>
    /// Shopee 店铺状态查询
    /// </summary>
    public class ShopeeShopStatusDto
    {
        public string AppKey { get; set; } = null!;
        public string AppSecret { get; set; } = null!;
        public string? AccessToken { get; set; }
    }

}
