using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim MigrateLock = new(1, 1);
    private static bool _migrated;

    public string? ConnectionString { get; } = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

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
            _migrated = true;
        }
        finally
        {
            MigrateLock.Release();
        }
    }
}

[CollectionDefinition("Postgres")]
public class PostgresTestCollection : ICollectionFixture<PostgresDatabaseFixture>;
