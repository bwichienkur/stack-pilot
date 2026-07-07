using System.Security.Claims;
using StackPilot.Application.Common;

namespace StackPilot.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                tenantContext.SetUser(userId);
        }

        if (context.Request.Headers.TryGetValue("X-Organization-Id", out var orgHeader) &&
            Guid.TryParse(orgHeader, out var orgId))
            tenantContext.SetOrganization(orgId);

        if (context.Request.Headers.TryGetValue("X-Workspace-Id", out var wsHeader) &&
            Guid.TryParse(wsHeader, out var wsId))
            tenantContext.SetWorkspace(wsId);

        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantMiddleware>();
}
