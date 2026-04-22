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
    }
}
