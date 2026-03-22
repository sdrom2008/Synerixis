using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    /// <summary>
    /// 客服账号（独立于Seller，卖家雇佣的客服人员）
    /// 客服通过NexusAI平台登录，处理分配给他们的对话
    /// </summary>
    public class Agent : AggregateRoot<Guid>
    {
        public string Email { get; private set; } = string.Empty;      // 登录邮箱（也可支持手机号）
        public string? Phone { get; private set; }                    // 手机号（可选）
        public string PasswordHash { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;      // 客服姓名
        public string? AvatarUrl { get; private set; }                // 头像
        public AgentRole Role { get; private set; } = AgentRole.Agent; // Agent / Supervisor / Admin
        public bool IsActive { get; private set; } = true;            // 是否在线可用
        public bool IsOnline { get; private set; } = false;           // 当前在线状态
        public int MaxConcurrentSessions { get; private set; } = 5;   // 最大同时处理会话数
        public DateTime? LastLoginAt { get; private set; }
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; private set; }

        // 关联：所属店铺（Seller）
        public Guid ShopId { get; private set; }
        public Seller? Shop { get; private set; }

        // 导航：当前活跃会话（不持久化，运行时计算）
        public int CurrentSessionCount { get; set; } = 0;

        private Agent() { }

        /// <summary>
        /// 创建客服账号
        /// </summary>
        public static Agent Create(Guid shopId, string email, string name, string passwordHash, AgentRole role = AgentRole.Agent)
        {
            return new Agent
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                Email = email,
                Name = name,
                PasswordHash = passwordHash,
                Role = role,
                IsActive = true,
                IsOnline = false,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 更新密码
        /// </summary>
        public void UpdatePassword(string newPasswordHash)
        {
            PasswordHash = newPasswordHash;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 设置在线状态
        /// </summary>
        public void SetOnline(bool isOnline)
        {
            IsOnline = isOnline;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 激活/禁用账号
        /// </summary>
        public void SetActive(bool isActive)
        {
            IsActive = isActive;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 更新个人资料
        /// </summary>
        public void UpdateProfile(string? name, string? avatarUrl)
        {
            if (!string.IsNullOrEmpty(name)) Name = name;
            if (!string.IsNullOrEmpty(avatarUrl)) AvatarUrl = avatarUrl;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 记录登录时间
        /// </summary>
        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 设置最大并发会话数
        /// </summary>
        public void SetMaxConcurrentSessions(int max)
        {
            if (max <= 0) throw new ArgumentException("MaxConcurrentSessions must be positive");
            MaxConcurrentSessions = max;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 更新角色（仅限主管/管理员操作）
        /// </summary>
        public void UpdateRole(AgentRole newRole)
        {
            Role = newRole;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    public enum AgentRole
    {
        Agent = 1,        // 普通客服
        Supervisor = 2,   // 客服主管
        Admin = 3         // 系统管理员（跨店铺）
    }
}
