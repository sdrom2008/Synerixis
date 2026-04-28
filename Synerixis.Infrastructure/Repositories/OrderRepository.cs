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

        /// <summary>
        /// 【B2B】查询店铺下的订单列表（支持分页）
        /// </summary>
        public async Task<PaginatedList<Order>> GetByShopIdAsync(Guid shopId, int page = 1, int pageSize = 20)
        {
            var query = _context.Orders
                .Where(o => o.ShopId == shopId)
                .OrderByDescending(o => o.OrderTime);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedList<Order>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// 【B2C】查询特定买家在店铺下的订单
        /// </summary>
        public async Task<IEnumerable<Order>> GetByShopIdAndCustomerIdAsync(Guid shopId, string customerId)
        {
            return await _context.Orders
                .Where(o => o.ShopId == shopId && o.CustomerId == customerId)
                .OrderByDescending(o => o.OrderTime)
                .ToListAsync();
        }
    }
}
