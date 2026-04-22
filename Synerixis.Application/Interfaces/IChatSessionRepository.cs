using System.Threading.Tasks;
using Synerixis.Domain.Entities;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 客服会话仓储接口
    /// </summary>
    public interface IChatSessionRepository
    {
        /// <summary>
        /// 根据买家 ID 获取会话
        /// </summary>
        Task<ChatSession?> GetByCustomerIdAsync(string customerId);

        /// <summary>
        /// 根据 ID 获取会话（包含消息）
        /// </summary>
        Task<ChatSession?> GetByIdWithMessagesAsync(Guid id);

        /// <summary>
        /// 保存会话
        /// </summary>
        Task SaveAsync(ChatSession session);
    }
}
