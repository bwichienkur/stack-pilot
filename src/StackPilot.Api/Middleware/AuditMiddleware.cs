using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Middleware;

public class AuditMiddleware
{
    private static readonly HashSet<string> AuditedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        await _next(context);

        if (!AuditedMethods.Contains(context.Request.Method)) return;
        if (context.User.Identity?.IsAuthenticated != true) return;
        if (context.Response.StatusCode >= 400) return;

        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase)) return;
        if (path.Contains("/audit-logs", StringComparison.OrdinalIgnoreCase)) return;

        var action = $"http.{context.Request.Method.ToLowerInvariant()}";
        var details = System.Text.Json.JsonSerializer.Serialize(new
        {
            path,
            status = context.Response.StatusCode
        });

        await audit.LogAsync(action, "HttpRequest", details: details, ct: context.RequestAborted);
    }
}

public static class AuditMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<AuditMiddleware>();
}
