using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

/// <summary>
/// Runs against real PostgreSQL when POSTGRES_TEST_CONNECTION is set (CI backend job).
/// </summary>
[Collection("Postgres")]
public class PostgresIntegrationTests
{
    private readonly PostgresDatabaseFixture _fixture;

    public PostgresIntegrationTests(PostgresDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Postgres_Migrations_And_Rls_Policies_Apply()
    {
        if (string.IsNullOrWhiteSpace(_fixture.ConnectionString))
            return;

        await _fixture.EnsureMigratedAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var ctx = new AppDbContext(options, new TenantContext());
        Assert.True(await ctx.Database.CanConnectAsync());

        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_policies WHERE tablename = 'Tickets'";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.True(count > 0, "Expected RLS policies on Tickets table");
    }
}
