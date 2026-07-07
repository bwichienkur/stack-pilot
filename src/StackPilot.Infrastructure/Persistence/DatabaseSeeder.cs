using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        if (await db.Permissions.AnyAsync()) return;

        var permissions = new[]
        {
            "org:manage", "users:manage", "connectors:manage", "connectors:read",
            "graph:read", "graph:manage", "docs:read", "docs:manage",
            "tickets:create", "tickets:read", "tickets:manage",
            "tickets:approve:technical", "tickets:approve:security", "tickets:approve:database",
            "tickets:qa", "tickets:uat", "tickets:approve:release",
            "recommendations:read", "recommendations:manage",
            "deployments:read", "deployments:manage",
            "audit:read", "settings:manage", "dashboard:read", "ai:use"
        };

        foreach (var code in permissions)
            db.Permissions.Add(new Permission { Code = code, Description = code });

        var roles = new Dictionary<SystemRole, string[]>
        {
            [SystemRole.PlatformSuperAdmin] = permissions,
            [SystemRole.ClientAdmin] = ["org:manage", "users:manage", "connectors:manage", "connectors:read", "settings:manage", "audit:read", "dashboard:read", "tickets:create", "tickets:read", "tickets:manage", "tickets:approve:technical", "tickets:approve:security", "tickets:approve:database", "tickets:approve:release", "tickets:qa", "tickets:uat", "graph:read", "graph:manage", "docs:read", "docs:manage", "recommendations:read", "recommendations:manage", "deployments:read", "deployments:manage", "ai:use"],
            [SystemRole.Architect] = ["graph:read", "graph:manage", "docs:read", "docs:manage", "tickets:read", "tickets:approve:technical", "recommendations:read", "recommendations:manage", "dashboard:read", "ai:use", "connectors:read"],
            [SystemRole.Developer] = ["graph:read", "docs:read", "tickets:create", "tickets:read", "tickets:manage", "connectors:read", "ai:use", "dashboard:read"],
            [SystemRole.Qa] = ["tickets:read", "tickets:qa", "dashboard:read"],
            [SystemRole.UatApprover] = ["tickets:read", "tickets:uat", "dashboard:read"],
            [SystemRole.BusinessRequester] = ["tickets:create", "tickets:read", "dashboard:read", "ai:use"],
            [SystemRole.ReadOnlyExecutive] = ["graph:read", "docs:read", "tickets:read", "dashboard:read", "recommendations:read", "deployments:read", "audit:read"]
        };

        foreach (var (roleType, perms) in roles)
        {
            var role = new Role
            {
                Name = roleType.ToString(),
                Description = $"System role: {roleType}",
                IsSystem = true,
                SystemRoleType = roleType
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var permEntities = await db.Permissions.Where(p => perms.Contains(p.Code)).ToListAsync();
            foreach (var perm in permEntities)
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
        }

        var connectorDefs = new[]
        {
            new ConnectorDefinition
            {
                Type = "github_repository", Name = "GitHub Repository",
                Description = "Connect to GitHub repositories for code scanning and indexing",
                ConfigSchema = """{"type":"object","properties":{"owner":{"type":"string"},"repositories":{"type":"string"}}}""",
                Capabilities = """["repository_scan","code_indexing","webhook"]"""
            },
            new ConnectorDefinition
            {
                Type = "github_actions", Name = "GitHub Actions",
                Description = "Track CI/CD builds and deployments via GitHub Actions",
                ConfigSchema = """{"type":"object","properties":{"owner":{"type":"string"},"repositories":{"type":"string"}}}""",
                Capabilities = """["cicd_tracking","deployment_tracking","webhook"]"""
            },
            new ConnectorDefinition
            {
                Type = "sql_server", Name = "SQL Server",
                Description = "Connect to SQL Server databases for schema discovery",
                ConfigSchema = """{"type":"object","properties":{"server":{"type":"string"},"databases":{"type":"string"}}}""",
                Capabilities = """["database_scan"]"""
            },
            new ConnectorDefinition
            {
                Type = "postgresql", Name = "PostgreSQL",
                Description = "Connect to PostgreSQL databases for schema discovery",
                ConfigSchema = """{"type":"object","properties":{"host":{"type":"string"},"port":{"type":"string"},"databases":{"type":"string"}}}""",
                Capabilities = """["database_scan"]"""
            },
            new ConnectorDefinition
            {
                Type = "gitlab_repository", Name = "GitLab Repository",
                Description = "Connect to GitLab projects for code scanning and CI/CD tracking",
                ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"group":{"type":"string"},"projects":{"type":"string"}}}""",
                Capabilities = """["repository_scan","code_indexing","cicd_tracking"]"""
            }
        };

        db.ConnectorDefinitions.AddRange(connectorDefs);
        await db.SaveChangesAsync();
    }

    public static async Task EnsureConnectorDefinitionsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.ConnectorDefinitions.AnyAsync(d => d.Type == "gitlab_repository"))
        {
            db.ConnectorDefinitions.Add(new ConnectorDefinition
            {
                Type = "gitlab_repository",
                Name = "GitLab Repository",
                Description = "Connect to GitLab projects for code scanning and CI/CD tracking",
                ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"group":{"type":"string"},"projects":{"type":"string"}}}""",
                Capabilities = """["repository_scan","code_indexing","cicd_tracking"]"""
            });
            await db.SaveChangesAsync();
        }
    }
}
