using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Text.Json;

namespace Synerixis.Api.Exceptions
{
    /// <summary>
    /// 全局异常处理器
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IServiceProvider _serviceProvider;

        public GlobalExceptionHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 设置响应状态码
                httpContext.Response.StatusCode = (int)GetErrorStatusCode(exception);
                httpContext.Response.ContentType = "application/json; charset=utf-8";

                // 写入 JSON 错误响应
                var responseBody = new
                {
                    success = false,
                    message = FormatErrorMessage(exception, httpContext.Request.Path),
                    errorCode = exception.GetType().Name,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(responseBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                await httpContext.Response.WriteAsync(json, cancellationToken);
                return true;
            }
            catch
            {
                // 如果异常处理失败，返回 false 让后续处理器处理
                return false;
            }
        }

        private HttpStatusCode GetErrorStatusCode(Exception exception) => exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            _ when exception.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase) => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        private string FormatErrorMessage(Exception exception, PathString requestPath)
        {
            if (requestPath.StartsWithSegments("/admin") || requestPath.StartsWithSegments("/api/internal"))
                return exception.Message;

            return $"系统错误 ({exception.GetType().Name})";
        }
    }
}
