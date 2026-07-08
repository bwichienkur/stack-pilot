using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

/// <summary>
/// Verifies Postgres RLS tenant isolation when stackpilot.organization_id is set.
/// </summary>
public class PostgresRlsIsolationTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    [Fact]
    public async Task Postgres_Rls_Isolates_Tickets_By_Organization()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        var setupTenant = new TenantContext();
        setupTenant.DisableTenantFilter();
        await using var setup = new AppDbContext(options, setupTenant);
        await setup.Database.MigrateAsync();

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var wsA = Guid.NewGuid();
        var wsB = Guid.NewGuid();

        setup.Organizations.AddRange(
            new Organization { Id = orgA, Name = "Org A", Slug = $"org-a-{orgA:N}"[..12], Plan = OrganizationPlan.Trial },
            new Organization { Id = orgB, Name = "Org B", Slug = $"org-b-{orgB:N}"[..12], Plan = OrganizationPlan.Trial });
        setup.Workspaces.AddRange(
            new Workspace { Id = wsA, OrganizationId = orgA, Name = "WS A", Slug = "ws-a" },
            new Workspace { Id = wsB, OrganizationId = orgB, Name = "WS B", Slug = "ws-b" });
        setup.Tickets.AddRange(
            new Ticket { OrganizationId = orgA, WorkspaceId = wsA, Title = "Ticket A", TicketType = TicketType.Bug, RequesterId = Guid.NewGuid(), Status = TicketStatus.Submitted },
            new Ticket { OrganizationId = orgB, WorkspaceId = wsB, Title = "Ticket B", TicketType = TicketType.Bug, RequesterId = Guid.NewGuid(), Status = TicketStatus.Submitted });
        await setup.SaveChangesAsync();

        await using var tenantA = new AppDbContext(options, CreateTenant(orgA));
        await tenantA.SetOrganizationAsync(orgA);
        var ticketsA = await tenantA.Tickets.AsNoTracking().Select(t => t.Title).ToListAsync();
        Assert.Contains("Ticket A", ticketsA);
        Assert.DoesNotContain("Ticket B", ticketsA);

        await using var tenantB = new AppDbContext(options, CreateTenant(orgB));
        await tenantB.SetOrganizationAsync(orgB);
        var ticketsB = await tenantB.Tickets.AsNoTracking().Select(t => t.Title).ToListAsync();
        Assert.Contains("Ticket B", ticketsB);
        Assert.DoesNotContain("Ticket A", ticketsB);
    }

    private static TenantContext CreateTenant(Guid orgId)
    {
        var ctx = new TenantContext();
        ctx.DisableTenantFilter();
        ctx.SetOrganization(orgId);
        return ctx;
    }
}
