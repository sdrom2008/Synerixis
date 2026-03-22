using System;
using System.Security.Claims;

namespace Synerixis.Api.Helpers
{
    public class CurrentUser
    {
        public Guid UserId { get; set; }
        public string UserType { get; set; } = "Seller"; // Seller, Agent, Supervisor
        public Guid? ShopId { get; set; }

        public bool IsSeller => UserType == "Seller";
        public bool IsAgent => UserType == "Agent";
        public bool IsSupervisor => UserType == "Supervisor";
    }
}
