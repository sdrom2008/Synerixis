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
    }

    // 绑定手机
    public class BindPhoneDto
    {
        public string OpenId { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Code { get; set; } = null!;
    }

    // 解密手机号
    public class DecryptPhoneDto
    {
        public string Code { get; set; } = null!;
        public string EncryptedData { get; set; } = null!;
        public string Iv { get; set; } = null!;
        public string OpenId { get; set; } = null!;
    }
}
