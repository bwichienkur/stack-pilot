using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackPilot.Application.Common;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private const string AppRoleName = "stackpilot_app";
    private const string AppRolePassword = "stackpilot_app_test";

    private static readonly SemaphoreSlim MigrateLock = new(1, 1);
    private static bool _migrated;

    public string? ConnectionString { get; } = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    /// <summary>
    /// Non-superuser connection for RLS assertions. CI Postgres admin users bypass RLS.
    /// </summary>
    public string? AppConnectionString =>
        string.IsNullOrWhiteSpace(ConnectionString) ? null : BuildAppConnectionString(ConnectionString);

    public async Task InitializeAsync() => await EnsureMigratedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task EnsureMigratedAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;

        await MigrateLock.WaitAsync();
        try
        {
            if (_migrated)
                return;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            await using var ctx = new AppDbContext(options, new TenantContext());
            await ctx.Database.MigrateAsync();
            await EnsureAppRoleAsync(ConnectionString);
            _migrated = true;
        }
        finally
        {
            MigrateLock.Release();
        }
    }

    private static string BuildAppConnectionString(string adminConnectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Username = AppRoleName,
            Password = AppRolePassword
        };
        return builder.ConnectionString;
    }

    private static async Task EnsureAppRoleAsync(string adminConnectionString)
    {
        await using var conn = new NpgsqlConnection(adminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DO $rls$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{AppRoleName}') THEN
                CREATE ROLE {AppRoleName} WITH LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS;
              END IF;
            END
            $rls$;
            GRANT USAGE ON SCHEMA public TO {AppRoleName};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRoleName};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {AppRoleName};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
              GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppRoleName};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
              GRANT USAGE, SELECT ON SEQUENCES TO {AppRoleName};
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Postgres")]
public class PostgresTestCollection : ICollectionFixture<PostgresDatabaseFixture>;
