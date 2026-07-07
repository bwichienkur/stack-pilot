using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Security;

public class PermissionValidator : IPermissionValidator
{
    private readonly AppDbContext _db;

    public PermissionValidator(AppDbContext db) => _db = db;

    public async Task<bool> UserHasPermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default)
    {
        return await _db.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.OrganizationId == organizationId)
            .SelectMany(m => m.Role.RolePermissions)
            .AnyAsync(rp => rp.Permission.Code == permission, ct);
    }

    public async Task EnsurePermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default)
    {
        if (!await UserHasPermissionAsync(userId, organizationId, permission, ct))
            throw new UnauthorizedAccessException($"Missing required permission: {permission}");
    }
}
