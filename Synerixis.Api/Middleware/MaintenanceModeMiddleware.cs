using System.Security.Claims;
using System.Text.Json;
using Synerixis.Application.Interfaces;

namespace Synerixis.Api.Middleware
{
    /// <summary>
    /// 维护模式闸流量：放行 /health*、/api/admin/*、/api/auth/agent-login、/api/webhook*；
    /// 拦截非 Admin 的 /api/merchant/*、/api/seller/*（读写一律 503）。
    /// 新注册拦截在 AuthController（phone-login 自动建号）细判。
    /// </summary>
    public sealed class MaintenanceModeMiddleware
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<MaintenanceModeMiddleware> _logger;

        public MaintenanceModeMiddleware(RequestDelegate next, ILogger<MaintenanceModeMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISystemSettingsService settings)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (IsAlwaysAllowed(path))
            {
                var opsForFlag = await settings.GetOpsAsync(context.RequestAborted);
                if (opsForFlag.MaintenanceMode)
                    context.Items["MaintenanceMode"] = true;
                await _next(context);
                return;
            }

            var ops = await settings.GetOpsAsync(context.RequestAborted);
            if (ops.MaintenanceMode)
                context.Items["MaintenanceMode"] = true;

            if (ops.MaintenanceMode && IsMerchantSurface(path) && !IsAdminCaller(context.User))
            {
                _logger.LogInformation("[Maintenance] Blocked {Method} {Path}", context.Request.Method, path);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    code = "MAINTENANCE",
                    message = "系统维护中，请稍后再试"
                }, JsonOpts));
                return;
            }

            await _next(context);
        }

        private static bool IsAlwaysAllowed(string path)
        {
            if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.Equals("/api/auth/agent-login", StringComparison.OrdinalIgnoreCase))
                return true;
            if (path.StartsWith("/api/webhook", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static bool IsMerchantSurface(string path) =>
            path.StartsWith("/api/merchant", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/seller", StringComparison.OrdinalIgnoreCase);

        private static bool IsAdminCaller(ClaimsPrincipal user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return false;
            var role = user.FindFirst("userType")?.Value
                ?? user.FindFirst(ClaimTypes.Role)?.Value
                ?? user.FindFirst("role")?.Value;
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
