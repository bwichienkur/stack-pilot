using Microsoft.AspNetCore.Authorization;
using StackPilot.Application.Common;

namespace StackPilot.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ITenantContext _tenant;

    public PermissionAuthorizationHandler(ITenantContext tenant) => _tenant = tenant;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        if (_tenant.OrganizationId is null)
            return Task.CompletedTask;

        var orgId = _tenant.OrganizationId.Value.ToString();
        var permValue = $"{orgId}:{requirement.Permission}";

        if (context.User.HasClaim(StackPilotClaimTypes.OrgPermission, permValue))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.HasClaim(StackPilotClaimTypes.OrgRole, $"{orgId}:PlatformSuperAdmin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) => Policy = $"perm:{permission}";
}
