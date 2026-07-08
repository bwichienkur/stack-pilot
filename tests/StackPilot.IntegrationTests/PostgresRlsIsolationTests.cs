using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

/// <summary>
/// Verifies Postgres RLS tenant isolation when stackpilot.organization_id is set.
/// </summary>
[Collection("Postgres")]
public class PostgresRlsIsolationTests
{
    private readonly PostgresDatabaseFixture _fixture;

    public PostgresRlsIsolationTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Postgres_Rls_Isolates_Tickets_By_Organization()
    {
        if (string.IsNullOrWhiteSpace(_fixture.ConnectionString))
            return;

        await _fixture.EnsureMigratedAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        var setupTenant = new TenantContext();
        setupTenant.DisableTenantFilter();
        await using var setup = new AppDbContext(options, setupTenant);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var titleA = $"RLS-A-{suffix}";
        var titleB = $"RLS-B-{suffix}";
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var wsA = Guid.NewGuid();
        var wsB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        setup.Users.AddRange(
            new ApplicationUser { Id = userA, Email = $"user-a-{suffix}@rls.test", PasswordHash = "x" },
            new ApplicationUser { Id = userB, Email = $"user-b-{suffix}@rls.test", PasswordHash = "x" });
        setup.Organizations.AddRange(
            new Organization { Id = orgA, Name = "Org A", Slug = $"org-a-{suffix}", Plan = OrganizationPlan.Trial },
            new Organization { Id = orgB, Name = "Org B", Slug = $"org-b-{suffix}", Plan = OrganizationPlan.Trial });
        setup.Workspaces.AddRange(
            new Workspace { Id = wsA, OrganizationId = orgA, Name = "WS A", Slug = $"ws-a-{suffix}" },
            new Workspace { Id = wsB, OrganizationId = orgB, Name = "WS B", Slug = $"ws-b-{suffix}" });
        setup.Tickets.AddRange(
            new Ticket { OrganizationId = orgA, WorkspaceId = wsA, Title = titleA, TicketType = TicketType.Bug, RequesterId = userA, Status = TicketStatus.Submitted },
            new Ticket { OrganizationId = orgB, WorkspaceId = wsB, Title = titleB, TicketType = TicketType.Bug, RequesterId = userB, Status = TicketStatus.Submitted });
        await setup.SaveChangesAsync();

        var appConn = _fixture.AppConnectionString ?? throw new InvalidOperationException("App connection not configured");
        var titlesA = await QueryTicketTitlesAsync(appConn, orgA, suffix);
        Assert.Contains(titleA, titlesA);
        Assert.DoesNotContain(titleB, titlesA);

        var titlesB = await QueryTicketTitlesAsync(appConn, orgB, suffix);
        Assert.Contains(titleB, titlesB);
        Assert.DoesNotContain(titleA, titlesB);
    }

    private static async Task<List<string>> QueryTicketTitlesAsync(string connectionString, Guid orgId, string suffix)
    {
        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var setCmd = conn.CreateCommand();
        setCmd.CommandText = "SELECT set_config('stackpilot.organization_id', @orgId, false)";
        setCmd.Parameters.AddWithValue("orgId", orgId.ToString());
        await setCmd.ExecuteNonQueryAsync();

        await using var queryCmd = conn.CreateCommand();
        queryCmd.CommandText = """SELECT "Title" FROM "Tickets" WHERE "Title" LIKE @pattern""";
        queryCmd.Parameters.AddWithValue("pattern", $"RLS-%-{suffix}");
        var titles = new List<string>();
        await using var reader = await queryCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            titles.Add(reader.GetString(0));
        return titles;
    }
}
