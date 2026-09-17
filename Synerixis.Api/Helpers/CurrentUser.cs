using System;

namespace Synerixis.Api.Helpers
{
    public class CurrentUser
    {
        public Guid UserId { get; set; }
        /// <summary>Seller | Agent | Supervisor | Admin。
        /// Admin = PLATFORM Agents.Role=Admin（非商家「店长」角色名）。</summary>
        public string UserType { get; set; } = "Seller";
        public Guid? ShopId { get; set; }

        /// <summary>Platform Admin enter-merchant support/impersonation JWT（非商家坐席登录）。</summary>
        public bool IsSupport { get; set; }

        public bool IsSeller =>
            string.Equals(UserType, "Seller", StringComparison.OrdinalIgnoreCase);

        public bool IsAgent =>
            string.Equals(UserType, "Agent", StringComparison.OrdinalIgnoreCase);

        public bool IsSupervisor =>
            string.Equals(UserType, "Supervisor", StringComparison.OrdinalIgnoreCase);

        /// <summary>PLATFORM Admin JWT（Agents.Role=Admin）。进 merchant 时仍按挂靠 ShopId，不自动跨店。
        /// 跨店支持请走 Admin enter-merchant support token（IsSupport）。</summary>
        public bool IsAdmin =>
            string.Equals(UserType, "Admin", StringComparison.OrdinalIgnoreCase);

        /// <summary>坐席侧任意角色（Agent / Supervisor / 平台 Admin 挂靠本店）</summary>
        public bool IsStaff => IsAgent || IsSupervisor || IsAdmin;

        /// <summary>可管理本店团队：Seller / Supervisor / 平台 Admin（挂靠本店）；不可创建 Role=Admin；support token 否</summary>
        public bool CanManageTeam => (IsSeller || IsSupervisor || IsAdmin) && !IsSupport;
    }
}
