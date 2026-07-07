using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Connectors;
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

        db.ConnectorDefinitions.AddRange(ConnectorDefinitionCatalog.All.Select(d => ConnectorDefinitionCatalog.Create(d.Type)));
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

        var org = new Organization
        {
            Name = "Acme Corp",
            Slug = "acme-demo",
            SettingsJson = """{"featureFlags":{"applications":true,"docs":true,"recommendations":true,"qa":true,"uat":true,"deployments":true}}"""
        };
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

        db.ReleaseSchedules.Add(new ReleaseSchedule
        {
            OrganizationId = org.Id,
            TicketId = ticket.Id,
            ScheduledAt = DateTime.UtcNow.AddDays(7),
            ReleaseWindow = "Saturday 02:00–04:00 UTC",
            Status = ReleaseStatus.Scheduled,
            CreatedByUserId = user.Id,
            RollbackPlan = "Revert deployment and restore previous auth configuration"
        });

        await db.SaveChangesAsync();
        await approvalGates.EnsureDefaultGatesAsync(org.Id);
    }

    public static async Task EnsureConnectorDefinitionsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.ConnectorDefinitions.ToListAsync();
        var changed = false;

        foreach (var template in ConnectorDefinitionCatalog.All)
        {
            var def = existing.FirstOrDefault(d => d.Type == template.Type);
            if (def is null)
            {
                db.ConnectorDefinitions.Add(ConnectorDefinitionCatalog.Create(template.Type));
                changed = true;
            }
            else if (def.Category != template.Category || def.Name != template.Name || def.Description != template.Description)
            {
                def.Category = template.Category;
                def.Name = template.Name;
                def.Description = template.Description;
                def.ConfigSchema = template.ConfigSchema;
                def.Capabilities = template.Capabilities;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }
}
