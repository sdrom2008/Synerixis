using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    public class Seller : AggregateRoot<Guid>
    {
        public string OpenId { get; private set; } = string.Empty;          // 微信 OpenId，唯一标识
        public string? Phone { get; private set; }                          //手机号 主登录
        public string? Username { get; private set; }                       // 可选用户名
        public string? Email { get; private set; }                          // 可选邮箱
        public string? PasswordHash { get; private set; }                   // 可选本地登录用
        public string? Nickname { get; private set; }                       // 昵称（微信返回或手动设置）
        public string? AvatarUrl { get; private set; }                      // 头像
        public bool IsActive { get; private set; } = true;                  // 是否激活
        public string SubscriptionLevel { get; private set; } = "Free";     // Free / Basic / Pro
        public int? FreeQuota { get; private set; } = 100;                  //默认免费100条
        public DateTime? SubscriptionEnd { get; private set; }              //付费订阅到期时间（null 表示未付费）
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public string? RegisterSource { get; private set; }                 //注册来源：phone / wechat
        public DateTime? LastLoginAt { get; private set; }
        public string? LastLoginType { get; private set; }                //最后登录方式：wechat / phone
        public DateTime? UpdatedAt { get; private set; }

        public ICollection<SellerProduct> SellerProducts { get; set; } = new List<SellerProduct>();

        // 反��导航：该商家的所有会话
        public List<Conversation> Conversations { get; private set; } = new();

        // 反向导航：该商家的所有平台绑定
        public ICollection<PlatformConnection> PlatformConnections { get; set; } = new List<PlatformConnection>();

        public SellerConfig? Config { get; set; }  // 导航属性（可选）

        private Seller() { }  // EF Core 无参构造

        public static Seller Create(string openId, string? nickname = null, string? avatarUrl = null)
        {
            return new Seller
            {
                Id = Guid.NewGuid(),
                OpenId = openId,
                Nickname = nickname,
                AvatarUrl = avatarUrl
            };
        }

        // 手机号登录/注册时创建新商户
        public static Seller CreateWithPhone(string phone)
        {
            var id = Guid.NewGuid();
            return new Seller
            {
                Id = id,
                OpenId = "phone_" + id.ToString("N"), // 防止数据库 OpenId 唯一索引冲突
                Phone = phone,
                Nickname = "商户" + DateTime.Now.ToString("MMddHHmm"),
                FreeQuota = 100,  // 赠送免费额度
                SubscriptionLevel = "trial",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                RegisterSource = "phone"
            };
        }

        // 绑定手机号（已有微信账号时）
        public void BindPhone(string phone)
        {
            if (!string.IsNullOrEmpty(Phone))
                throw new InvalidOperationException("手机号已绑定，不可重复设置");

            Phone = phone;
        }

        // 绑定微信 openid（已有手机号账号时）
        public void BindWechat(string openId)
        {
            if (!string.IsNullOrEmpty(OpenId) && !OpenId.StartsWith("phone_"))
                throw new InvalidOperationException("微信已绑定，不可重复设置");

            OpenId = openId;
        }

        // 消耗免费额度（聊天时调用）
        public void ConsumeQuota()
        {
            if (FreeQuota <= 0)
                throw new InvalidOperationException("免费额度已用完，请续费");

            FreeQuota--;
        }

        // 记录登录（统一更新 LastLoginAt 和 LastLoginType）
        public void RecordLogin(string loginType)
        {
            LastLoginAt = DateTime.UtcNow;
            LastLoginType = loginType;
        }

        public void UpdateProfile(string? nickname, string? avatarUrl)
        {
            Nickname = nickname ?? Nickname;
            AvatarUrl = avatarUrl ?? AvatarUrl;
            UpdatedAt = DateTime.UtcNow;
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpgradeSubscription(string newLevel)
        {
            if (!new[] { "Free", "Basic", "Pro" }.Contains(newLevel))
                throw new ArgumentException("无效的订阅等级");

            SubscriptionLevel = newLevel;
            UpdatedAt = DateTime.UtcNow;
        }

        // 应用订阅（支付成功后调用）
        public void ApplySubscription(decimal amount)
        {
            // 月付 99 元权益
            this.FreeQuota += 1000;
            this.SubscriptionEnd = DateTime.UtcNow.AddDays(30);
            this.SubscriptionLevel = "Pro";
            this.UpdatedAt = DateTime.UtcNow;
        }

    }
}
