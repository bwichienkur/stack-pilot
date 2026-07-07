using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

/// <summary>
/// Runs against real PostgreSQL when POSTGRES_TEST_CONNECTION is set (CI backend job).
/// </summary>
public class PostgresIntegrationTests
{
    private readonly string? _connectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    [Fact]
    public async Task Postgres_Migrations_And_Rls_Policies_Apply()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await using var ctx = new AppDbContext(options, new TenantContext());
        await ctx.Database.MigrateAsync();
        Assert.True(await ctx.Database.CanConnectAsync());

        await using var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pg_policies WHERE tablename = 'Tickets'";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.True(count > 0, "Expected RLS policies on Tickets table");
    }
}
