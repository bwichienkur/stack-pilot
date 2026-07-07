using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Interfaces;
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
            },
            new ConnectorDefinition
            {
                Type = "jira", Name = "Jira",
                Description = "Connect to Jira Cloud for project and issue tracking",
                ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"projects":{"type":"string"}}}""",
                Capabilities = """["ticket_sync"]"""
            }
        };

        db.ConnectorDefinitions.AddRange(connectorDefs);
        await db.SaveChangesAsync();
    }

    public static async Task SeedDemoDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var approvalGates = scope.ServiceProvider.GetRequiredService<IApprovalGateService>();

        if (await db.Users.AnyAsync(u => u.Email == "demo@stackpilot.dev")) return;

        var user = new ApplicationUser
        {
            Email = "demo@stackpilot.dev",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("DemoPassword123!"),
            FirstName = "Demo",
            LastName = "User"
        };
        db.Users.Add(user);

        var org = new Organization { Name = "Acme Corp", Slug = "acme-demo" };
        db.Organizations.Add(org);

        var adminRole = await db.Roles.FirstAsync(r => r.SystemRoleType == SystemRole.ClientAdmin);
        db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = org.Id, UserId = user.Id, RoleId = adminRole.Id });

        var workspace = new Workspace { OrganizationId = org.Id, Name = "Production", Slug = "production", Description = "Demo workspace" };
        db.Workspaces.Add(workspace);

        var appNode = new GraphNode { OrganizationId = org.Id, WorkspaceId = workspace.Id, NodeType = GraphNodeType.Application, Name = "Customer Portal", RiskScore = 3.5m };
        db.GraphNodes.Add(appNode);

        var ticket = new Ticket
        {
            OrganizationId = org.Id,
            WorkspaceId = workspace.Id,
            Title = "Add two-factor authentication",
            Description = "Users need 2FA for compliance with SOC2",
            TicketType = TicketType.NewFeature,
            Priority = TicketPriority.High,
            Status = TicketStatus.AwaitingApproval,
            RequesterId = user.Id,
            BusinessJustification = "SOC2 audit requirement",
            AiRequirementsJson = """{"businessSummary":"Implement TOTP-based 2FA","functionalRequirements":"Users can enroll authenticator apps\nLogin requires OTP after password","nonFunctionalRequirements":"99.9% availability","acceptanceCriteria":"2FA enrollment works\nLogin blocked without OTP","citations":[{"nodeId":"","excerpt":"Customer Portal auth module"}]}""",
            RiskScore = 5.5m,
            ConfidenceScore = 0.88m
        };
        db.Tickets.Add(ticket);

        db.DocumentationPages.Add(new DocumentationPage
        {
            OrganizationId = org.Id,
            WorkspaceId = workspace.Id,
            Title = "Customer Portal Architecture",
            DocType = "Architecture"
        });

        await db.SaveChangesAsync();
        await approvalGates.EnsureDefaultGatesAsync(org.Id);
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

        if (!await db.ConnectorDefinitions.AnyAsync(d => d.Type == "jira"))
        {
            db.ConnectorDefinitions.Add(new ConnectorDefinition
            {
                Type = "jira",
                Name = "Jira",
                Description = "Connect to Jira Cloud for project and issue tracking",
                ConfigSchema = """{"type":"object","properties":{"baseUrl":{"type":"string"},"projects":{"type":"string"}}}""",
                Capabilities = """["ticket_sync"]"""
            });
            await db.SaveChangesAsync();
        }
    }
}
