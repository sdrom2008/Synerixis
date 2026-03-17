using Synerixis.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Synerixis.Domain.Entities
{
    public class Conversation : AggregateRoot<Guid>
    {
        public string Title { get; private set; } = "新对话";
        public List<ChatMessage> Messages { get; private set; } = new();
        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime? LastActiveAt { get; private set; }

        public DateTime? LastViewedAt { get; private set; }  // 最后查看时间

        // 外键：关联 Seller（商家）
        public string Platform { get; private set; } // e.g., "TAOBAO", "DOUYIN"
        public Guid SellerId { get; private set; }
        // 导航属性（可选，但推荐加，便于查询）
        public Seller? Seller { get; private set; }

        /// <summary>
        /// 删除标记
        /// </summary>
        public bool IsDeleted { get; private set; } = false;


        // 会话唯一标识（如果你需要一个字符串形式的 ID，比如前端用）
        //public Guid ConversationId { get; private set; }  // 新增这个
        // 或者如果你想在数据库里用 JSON 列存储 Messages（不推荐长期用，建议用单独表）
        // public string MessagesJson { get; private set; } = "[]";

        private Conversation() { }

        public static Conversation Create(Guid sellerId, string platform, string? title = null)
        {
            var conv = new Conversation
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId,
                Platform = platform,
                Title = title ?? "新对话"
            };
            Console.WriteLine("创建 Conversation，内存 Id = " + conv.Id);
            return conv;
        }

        public void AddMessage(ChatMessage message)
        {
            Messages.Add(message);
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 
        /// </summary>
        public void MarkAsViewed()
        {
            LastViewedAt = DateTime.UtcNow;
            LastActiveAt = DateTime.UtcNow;
        }

        /// <summary>
        /// 删除会话
        /// </summary>
        public void MarkAsDeleted()
        {
            IsDeleted = true;
            // 可选：更新 LastActiveAt 或其他状态
            LastActiveAt = DateTime.UtcNow;
        }
    }
}
