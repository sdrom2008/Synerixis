using Microsoft.AspNetCore.Http;
using System;

namespace Synerixis.Api
{
    /// <summary>
    /// HttpContext 工厂（用于支持全局异常处理）
    /// </summary>
    public class HttpContextFactory
    {
        readonly IServiceProvider _serviceProvider;

        public HttpContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public HttpContext Create(HttpContextAccessor accessor)
        {
            return accessor.HttpContext;
        }
    }
}
