using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 订单仓储接口
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// 根据店铺和外部订单号查找订单
        /// </summary>
        Task<Order?> GetOrderByShopAndExternalOrderIdAsync(Guid shopId, string externalOrderId);
        
        /// <summary>
        /// 根据店铺和平台查找最近订单
        /// </summary>
        Task<Order?> GetRecentOrderForCustomerAsync(Guid shopId, string platform, string customerId);

        /// <summary>
        /// 【B2B】查询店铺下的所有订单（商家后台/自助查询用）
        /// </summary>
        Task<PaginatedList<Order>> GetByShopIdAsync(Guid shopId, int page = 1, int pageSize = 20);

        /// <summary>
        /// 【B2C】查询特定买家在店铺下的订单（AI 代查用）
        /// </summary>
        Task<IEnumerable<Order>> GetByShopIdAndCustomerIdAsync(Guid shopId, string customerId);
    }

    /// <summary>
    /// 简单的分页列表
    /// </summary>
    public class PaginatedList<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
