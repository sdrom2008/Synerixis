using System;

namespace Synerixis.Api.Helpers
{
    public class CurrentUser
    {
        public Guid UserId { get; set; }
        /// <summary>Seller | Agent | Supervisor | Admin</summary>
        public string UserType { get; set; } = "Seller";
        public Guid? ShopId { get; set; }

        public bool IsSeller =>
            string.Equals(UserType, "Seller", StringComparison.OrdinalIgnoreCase);

        public bool IsAgent =>
            string.Equals(UserType, "Agent", StringComparison.OrdinalIgnoreCase);

        public bool IsSupervisor =>
            string.Equals(UserType, "Supervisor", StringComparison.OrdinalIgnoreCase);

        public bool IsAdmin =>
            string.Equals(UserType, "Admin", StringComparison.OrdinalIgnoreCase);

        /// <summary>坐席侧任意角色（Agent / Supervisor / Admin）</summary>
        public bool IsStaff => IsAgent || IsSupervisor || IsAdmin;

        /// <summary>可管理本店团队：Seller / Supervisor / Admin</summary>
        public bool CanManageTeam => IsSeller || IsSupervisor || IsAdmin;
    }
}
