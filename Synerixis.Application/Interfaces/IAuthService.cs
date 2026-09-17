using System;

namespace Synerixis.Application.Interfaces
{
    public interface IAuthService
    {
        string GenerateJwt(Guid userId, string userType, Guid? shopId = null);

        /// <summary>
        /// Platform Admin enter-merchant support token: acts as Seller for target shop,
        /// short-lived, claims support/impersonation=true. Does not create a merchant Agent seat.
        /// </summary>
        string GenerateSupportJwt(Guid sellerShopId, Guid adminActorId, int expiryMinutes = 60);
    }
}
