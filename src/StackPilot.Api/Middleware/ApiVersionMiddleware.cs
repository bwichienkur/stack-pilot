namespace StackPilot.Api.Middleware;

public class ApiVersionMiddleware
{
    private readonly RequestDelegate _next;

    public ApiVersionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-API-Version"] = "1.0";
            context.Response.Headers["X-API-Supported-Versions"] = "1.0";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class ApiVersionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiVersionHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<ApiVersionMiddleware>();
}
