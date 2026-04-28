using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;

namespace Synerixis.Infrastructure.Repositories
{
    /// <summary>
    /// 平台连接仓储实现
    /// </summary>
    public class PlatformConnectionRepository : IPlatformConnectionRepository
    {
        private readonly AppDbContext _context;
        
        public PlatformConnectionRepository(AppDbContext context)
        {
            _context = context;
        }
        
        public async Task<PlatformConnection?> GetByIdAsync(Guid id)
        {
            return await _context.PlatformConnections.FindAsync(id);
        }
        
        public async Task<PlatformConnection?> GetBySellerIdAsync(Guid sellerId)
        {
            return await _context.PlatformConnections
                .FirstOrDefaultAsync(c => c.SellerId == sellerId);
        }
        
        public async Task<PlatformConnection?> GetByShopIdAndPlatformAsync(string shopId, string platform)
        {
            return await _context.PlatformConnections
                .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Platform == platform);
        }
        
        public async Task<IEnumerable<PlatformConnection>> GetBySellerIdAndActiveAsync(Guid sellerId)
        {
            return await _context.PlatformConnections
                .Where(c => c.SellerId == sellerId && c.IsActive)
                .ToListAsync();
        }
        
        public async Task AddAsync(PlatformConnection connection)
        {
            await _context.PlatformConnections.AddAsync(connection);
        }
        
        public async Task UpdateAsync(PlatformConnection connection)
        {
            _context.PlatformConnections.Update(connection);
        }
    }
}