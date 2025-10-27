using FeedHorn.Services;

namespace FeedHorn.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> PublicPaths = new()
    {
        "/login.html",
        "/api/auth/login",
        "/api/auth/status"
    };

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AuthService authService)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Allow public paths
        if (PublicPaths.Any(p => path.Contains(p)))
        {
            await _next(context);
            return;
        }

        // Allow static files (css, js, etc.)
        if (path.Contains(".css") || path.Contains(".js") || path.Contains(".svg") || path.Contains(".png"))
        {
            await _next(context);
            return;
        }

        // Check authentication
        if (!context.Request.Cookies.TryGetValue("FeedHorn_UserId", out var userIdStr) ||
            !int.TryParse(userIdStr, out var userId))
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
                return;
            }
            else
            {
                context.Response.Redirect("/login.html");
                return;
            }
        }

        // Verify user exists
        var user = await authService.GetUserById(userId);
        if (user == null)
        {
            context.Response.Cookies.Delete("FeedHorn_UserId");
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
                return;
            }
            else
            {
                context.Response.Redirect("/login.html");
                return;
            }
        }

        await _next(context);
    }
}
