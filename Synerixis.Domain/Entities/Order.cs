using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 订单缓存表
    /// 从电商平台API同步过来的订单数据，用于客服快速查询
    /// </summary>
    public class Order : AggregateRoot<Guid>
    {
        public string OrderNo { get; private set; } = string.Empty;           // 电商平台订单号
        public string? ExternalOrderId { get; private set; }                 // 外部系统订单ID（如淘宝订单ID）
        public Guid ShopId { get; private set; }                              // 所属店铺

        [MaxLength(191)]
        public string CustomerId { get; private set; }                          // 买家ID（与ChatSession的CustomerId对应）
        public string? CustomerName { get; private set; }                     // 买家昵称
        public string? CustomerPhone { get; private set; }                    // 买家手机

        // 订单状态：PendingPayment / Paid / Shipped / Received / Completed / Cancelled / Refunded
        public string Status { get; private set; } = "PendingPayment";

        // 金额
        public decimal TotalAmount { get; private set; }                      // 订单总金额
        public decimal? PaymentAmount { get; private set; }                  // 实付金额
        public decimal? RefundAmount { get; private set; }                   // 退款金额

        // 物流信息
        public string? LogisticsNo { get; private set; }                      // 快递单号
        public string? LogisticsCompany { get; private set; }                 // 快递公司
        public string? ShippingAddress { get; private set; }                  // 收货地址

        // 时间戳
        public DateTime OrderTime { get; private set; } = DateTime.UtcNow;    // 下单时间
        public DateTime? PaidAt { get; private set; }                        // 支付时间
        public DateTime? ShippedAt { get; private set; }                     // 发货时间
        public DateTime? ReceivedAt { get; private set; }                    // 确认收货时间
        public DateTime? CompletedAt { get; private set; }                   // 订单完成时间
        public DateTime SyncedAt { get; private set; } = DateTime.UtcNow;    // 同步时间（从平台拉取）
        public DateTime? UpdatedAt { get; private set; }                     // 最后更新时间

        public string? Platform { get; private set; }                         // TAOBAO, JD, DOUYIN

        // 导航属性
        public Seller? Shop { get; private set; }

        private Order() { }

        /// <summary>
        /// 创建订单缓存记录
        /// </summary>
        public static Order Create(
            Guid shopId,
            string orderNo,
            string customerId,
            string? customerName = null,
            decimal totalAmount = 0)
        {
            return new Order
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                OrderNo = orderNo,
                CustomerId = customerId,
                CustomerName = customerName,
                TotalAmount = totalAmount,
                Status = "PendingPayment",
                OrderTime = DateTime.UtcNow,
                SyncedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 更新订单状态（从平台同步）
        /// </summary>
        public void UpdateStatus(string status)
        {
            Status = status;
            UpdatedAt = DateTime.UtcNow;

            // 更新时间戳
            switch (status.ToLower())
            {
                case "paid":
                    PaidAt = DateTime.UtcNow;
                    break;
                case "shipped":
                    ShippedAt = DateTime.UtcNow;
                    break;
                case "received":
                    ReceivedAt = DateTime.UtcNow;
                    break;
                case "completed":
                    CompletedAt = DateTime.UtcNow;
                    break;
            }
        }

        /// <summary>
        /// 更新物流信息
        /// </summary>
        public void UpdateLogistics(string? logisticsNo, string? logisticsCompany)
        {
            LogisticsNo = logisticsNo;
            LogisticsCompany = logisticsCompany;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 本地演示/缓存补全字段（非平台同步权威）。
        /// </summary>
        public void ApplyLocalDemoDetails(
            string? platform = null,
            decimal? paymentAmount = null,
            string? customerPhone = null,
            string? shippingAddress = null,
            string? externalOrderId = null)
        {
            if (!string.IsNullOrWhiteSpace(platform)) Platform = platform.Trim();
            if (paymentAmount.HasValue) PaymentAmount = paymentAmount;
            if (customerPhone != null) CustomerPhone = customerPhone;
            if (shippingAddress != null) ShippingAddress = shippingAddress;
            if (externalOrderId != null) ExternalOrderId = externalOrderId;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 标记已退款
        /// </summary>
        public void MarkRefunded(decimal refundAmount)
        {
            Status = "Refunded";
            RefundAmount = refundAmount;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 同步订单（更新字段）
        /// </summary>
        public void SyncFromExternal(
            string status,
            decimal? totalAmount,
            string? logisticsNo,
            string? logisticsCompany)
        {
            if (totalAmount.HasValue) TotalAmount = totalAmount.Value;
            if (!string.IsNullOrEmpty(status)) Status = status;
            if (!string.IsNullOrEmpty(logisticsNo)) LogisticsNo = logisticsNo;
            if (!string.IsNullOrEmpty(logisticsCompany)) LogisticsCompany = logisticsCompany;

            SyncedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}

