using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using Synerixis.Application.Interfaces;

namespace Synerixis.Infrastructure.Repositories
{
    /// <summary>
    /// 订单仓储实现
    /// </summary>
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        
        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// 根据店铺和外部订单号查找订单
        /// </summary>
        public async Task<Order?> GetOrderByShopAndExternalOrderIdAsync(Guid shopId, string externalOrderId)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(o => o.ShopId == shopId && o.ExternalOrderId == externalOrderId);
        }
        
        /// <summary>
        /// 根据店铺和平台查找最近订单
        /// </summary>
        public async Task<Order?> GetRecentOrderForCustomerAsync(Guid shopId, string platform, string customerId)
        {
            return await _context.Orders
                .Where(o => o.ShopId == shopId && o.Platform == platform && o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderTime)
                .FirstOrDefaultAsync();
        }
    }
}
