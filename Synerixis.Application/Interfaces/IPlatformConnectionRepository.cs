using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 平台连接仓储接口
    /// </summary>
    public interface IPlatformConnectionRepository
    {
        Task<PlatformConnection?> GetByIdAsync(Guid id);
        Task<PlatformConnection?> GetBySellerIdAsync(Guid sellerId);
        Task<PlatformConnection?> GetByShopIdAndPlatformAsync(string shopId, string platform);
        Task<IEnumerable<PlatformConnection>> GetBySellerIdAndActiveAsync(Guid sellerId);
        Task AddAsync(PlatformConnection connection);
        Task UpdateAsync(PlatformConnection connection);
    }
}