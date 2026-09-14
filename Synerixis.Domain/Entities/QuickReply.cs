using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 快捷回复模板
    /// 客服常用话术，可全局或店铺级别配置
    /// </summary>
    public class QuickReply : AggregateRoot<Guid>
    {
        public string Title { get; private set; } = string.Empty;            // 标题/标签
        public string Content { get; private set; } = string.Empty;          // 回复内容
        public QuickReplyCategory Category { get; private set; } = QuickReplyCategory.General;
        public string? Keywords { get; private set; }                        // 触发关键词（逗号分隔）

        // 作用范围：Global（全局） / Shop（店铺级）
        public QuickReplyScope Scope { get; private set; } = QuickReplyScope.Global;
        public Guid? ShopId { get; private set; }                            // 如果Scope=Shop，则关联店铺

        public bool IsActive { get; private set; } = true;                   // 是否启用
        public int SortOrder { get; private set; } = 0;                      // 排序权重
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }

        // 导航
        public Seller? Shop { get; private set; }

        private QuickReply() { }

        /// <summary>
        /// 创建全局快捷回复
        /// </summary>
        public static QuickReply CreateGlobal(string title, string content, QuickReplyCategory category = QuickReplyCategory.General)
        {
            return new QuickReply
            {
                Id = Guid.NewGuid(),
                Title = title,
                Content = content,
                Category = category,
                Scope = QuickReplyScope.Global,
                IsActive = true,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 创建店铺级快捷回复
        /// </summary>
        public static QuickReply CreateForShop(
            Guid shopId,
            string title,
            string content,
            QuickReplyCategory category = QuickReplyCategory.General)
        {
            return new QuickReply
            {
                Id = Guid.NewGuid(),
                Title = title,
                Content = content,
                Category = category,
                Scope = QuickReplyScope.Shop,
                ShopId = shopId,
                IsActive = true,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 更新内容
        /// </summary>
        public void Update(string title, string content, string? keywords = null, QuickReplyCategory? category = null)
        {
            Title = title;
            Content = content;
            if (keywords != null) Keywords = keywords;
            if (category.HasValue) Category = category.Value;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 启用/禁用
        /// </summary>
        public void SetActive(bool isActive)
        {
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 设置排序
        /// </summary>
        public void SetSortOrder(int sortOrder)
        {
            SortOrder = sortOrder;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public enum QuickReplyCategory
    {
        General = 1,       // 通用
        PreSale = 2,       // 售前
        AfterSale = 3,     // 售后
        Logistics = 4,     // 物流
        Complaint = 5,     // 投诉
        Payment = 6        // 支付问题
    }

    public enum QuickReplyScope
    {
        Global = 1,        // 全局（所有店铺可用）
        Shop = 2           // 店铺级（仅特定店铺）
    }
}
