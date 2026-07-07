using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Api.Middleware;

public class TenantMiddleware
{
    private static readonly string[] OrgExemptPrefixes =
    [
        "/api/v1/auth",
        "/health",
        "/swagger",
        "/hangfire"
    ];

    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                tenantContext.SetUser(userId);
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var requiresOrg = context.User.Identity?.IsAuthenticated == true &&
                          !IsOrgExempt(path) &&
                          !IsBootstrapPath(context);

        if (requiresOrg)
        {
            if (!context.Request.Headers.TryGetValue("X-Organization-Id", out var orgHeader) ||
                !Guid.TryParse(orgHeader, out var orgId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                    new ApiError { Code = "ORG_REQUIRED", Message = "X-Organization-Id header is required" }));
                return;
            }

            if (tenantContext.UserId is Guid userId)
            {
                var isMember = await db.OrganizationMembers
                    .AsNoTracking()
                    .AnyAsync(m => m.OrganizationId == orgId && m.UserId == userId);

                if (!isMember)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                        new ApiError { Code = "ORG_ACCESS_DENIED", Message = "You are not a member of this organization" }));
                    return;
                }
            }

            tenantContext.SetOrganization(orgId);

            if (context.Request.Headers.TryGetValue("X-Workspace-Id", out var wsHeader) &&
                Guid.TryParse(wsHeader, out var wsId))
            {
                var workspaceValid = await db.Workspaces
                    .AsNoTracking()
                    .AnyAsync(w => w.Id == wsId && w.OrganizationId == orgId);

                if (!workspaceValid)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                        new ApiError { Code = "WORKSPACE_ACCESS_DENIED", Message = "Invalid workspace for organization" }));
                    return;
                }

                tenantContext.SetWorkspace(wsId);
            }
        }
        else if (context.Request.Headers.TryGetValue("X-Organization-Id", out var optionalOrg) &&
                 Guid.TryParse(optionalOrg, out var optionalOrgId) &&
                 tenantContext.UserId is Guid userId)
        {
            var isMember = await db.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(m => m.OrganizationId == optionalOrgId && m.UserId == userId);

            if (isMember)
                tenantContext.SetOrganization(optionalOrgId);
        }

        await _next(context);
    }

    private static bool IsOrgExempt(string path)
    {
        foreach (var prefix in OrgExemptPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsBootstrapPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        if (path.Equals("/api/v1/organizations", StringComparison.OrdinalIgnoreCase) &&
            (method == HttpMethods.Get || method == HttpMethods.Post))
            return true;

        if (path.Equals("/api/v1/connectors/definitions", StringComparison.OrdinalIgnoreCase) &&
            method == HttpMethods.Get)
            return true;

        return false;
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantMiddleware>();
}
