using Microsoft.AspNetCore.Authorization;
using StackPilot.Api.Authorization;
using StackPilot.Application.Common;

namespace StackPilot.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddStackPilotAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy($"perm:{permission}", policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        return services;
    }
}
