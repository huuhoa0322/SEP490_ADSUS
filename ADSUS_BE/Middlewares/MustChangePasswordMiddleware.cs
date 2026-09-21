using System.Security.Claims;

namespace ADSUS_BE.Middlewares;

public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth/change-password",
        "/api/v1/auth/logout"
    };

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChange = context.User.FindFirstValue("MustChangePassword");
            if (mustChange == "true" && !AllowedPaths.Contains(context.Request.Path.Value ?? ""))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { message = "Bạn phải đổi mật khẩu trước khi thực hiện thao tác này." });
                return;
            }
        }
        await _next(context);
    }
}
