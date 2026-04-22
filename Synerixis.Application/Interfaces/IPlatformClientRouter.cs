using Synerixis.Application.Interfaces;

namespace Synerixis.Application.Interfaces
{
    /// <summary>
    /// 平台客户端路由器 - 根据平台类型返回对应客户端实例
    /// Phase 1: Shopee + TikTok Shop
    /// Phase 2（可选）：Lazada、Amazon、AliExpress
    /// </summary>
    public interface IPlatformClientRouter
    {
        /// <summary>
        /// 获取指定平台的客户端
        /// </summary>
        IPlatformClient GetClient(string platform);

        /// <summary>
        /// 检查平台是否已注册
        /// </summary>
        bool IsSupported(string platform);
    }
}
