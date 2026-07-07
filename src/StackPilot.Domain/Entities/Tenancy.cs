using StackPilot.Domain.Common;
using StackPilot.Domain.Enums;

namespace StackPilot.Domain.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public OrganizationPlan Plan { get; set; } = OrganizationPlan.Trial;
    public bool IsActive { get; set; } = true;
    public string? SettingsJson { get; set; }

    public ICollection<Workspace> Workspaces { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
}

public class Workspace : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Organization Organization { get; set; } = null!;
    public ICollection<ConnectorInstance> Connectors { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}

public class ApplicationUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? ExternalId { get; set; }
    public string? AuthProvider { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
    public ICollection<OrganizationMember> Memberships { get; set; } = [];
}

public class OrganizationMember : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Organization Organization { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

public class Team : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Organization Organization { get; set; } = null!;
    public Workspace? Workspace { get; set; }
}

public class Role : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public SystemRole? SystemRoleType { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}

public class EnvironmentConfig : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // dev, test, staging, production
    public string? ConfigJson { get; set; }
    public bool IsProduction { get; set; }
}
