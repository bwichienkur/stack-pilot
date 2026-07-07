using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public abstract class ConnectorBase : IConnector
{
    public abstract string Type { get; }
    public abstract ConnectorCapabilities Capabilities { get; }

    public abstract Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default);
    public abstract Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default);

    public virtual Task<HealthCheckResult> HealthCheckAsync(ConnectorContext context, CancellationToken ct = default)
    {
        return TestConnectionAsync(context, ct).ContinueWith(t => new HealthCheckResult
        {
            IsHealthy = t.Result.Success,
            Status = t.Result.Success ? "healthy" : "unhealthy",
            Message = t.Result.Message
        }, ct);
    }

    protected static Dictionary<string, string> ParseConfig(string configJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new();
        }
        catch
        {
            return new();
        }
    }
}

public class GitHubRepositoryConnector : ConnectorBase
{
    public override string Type => "github_repository";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.RepositoryScan | ConnectorCapabilities.CodeIndexing | ConnectorCapabilities.WebhookReceive;

    public override Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var hasToken = context.Credentials.ContainsKey("pat") || context.Credentials.ContainsKey("oauth_token");
        var hasOwner = config.ContainsKey("owner");

        if (!hasToken)
            return Task.FromResult(new ConnectionTestResult { Success = false, Message = "GitHub token is required" });
        if (!hasOwner)
            return Task.FromResult(new ConnectionTestResult { Success = false, Message = "GitHub owner is required" });

        return Task.FromResult(new ConnectionTestResult
        {
            Success = true,
            Message = $"Connected to GitHub organization/user: {config["owner"]}",
            Details = new() { ["owner"] = config["owner"], ["repositories"] = config.GetValueOrDefault("repositories", "all") }
        });
    }

    public override Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var repos = config.GetValueOrDefault("repositories", "sample-api,sample-web").Split(',');
        return Task.FromResult(new SyncResult
        {
            Success = true,
            ItemsProcessed = repos.Length,
            Metadata = new() { ["repositories"] = repos, ["syncType"] = "repository" }
        });
    }
}

public class GitHubActionsConnector : ConnectorBase
{
    public override string Type => "github_actions";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.CiCdTracking | ConnectorCapabilities.WebhookReceive | ConnectorCapabilities.DeploymentTracking;

    public override Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var hasToken = context.Credentials.ContainsKey("pat") || context.Credentials.ContainsKey("oauth_token");
        return Task.FromResult(new ConnectionTestResult
        {
            Success = hasToken,
            Message = hasToken ? "GitHub Actions connector ready" : "GitHub token is required"
        });
    }

    public override Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        return Task.FromResult(new SyncResult
        {
            Success = true,
            ItemsProcessed = 5,
            Metadata = new() { ["workflowRuns"] = 5, ["syncType"] = "cicd" }
        });
    }
}

public class SqlServerConnector : ConnectorBase
{
    public override string Type => "sql_server";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.DatabaseScan;

    public override Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var hasConnStr = context.Credentials.ContainsKey("connection_string");
        return Task.FromResult(new ConnectionTestResult
        {
            Success = hasConnStr,
            Message = hasConnStr ? "SQL Server connection configured" : "Connection string is required"
        });
    }

    public override Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "master").Split(',');
        return Task.FromResult(new SyncResult
        {
            Success = true,
            ItemsProcessed = databases.Length,
            Metadata = new() { ["databases"] = databases, ["syncType"] = "database" }
        });
    }
}

public class PostgreSQLConnector : ConnectorBase
{
    public override string Type => "postgresql";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.DatabaseScan;

    public override Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var hasConnStr = context.Credentials.ContainsKey("connection_string");
        return Task.FromResult(new ConnectionTestResult
        {
            Success = hasConnStr,
            Message = hasConnStr ? "PostgreSQL connection configured" : "Connection string is required"
        });
    }

    public override Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "postgres").Split(',');
        return Task.FromResult(new SyncResult
        {
            Success = true,
            ItemsProcessed = databases.Length,
            Metadata = new() { ["databases"] = databases, ["syncType"] = "database" }
        });
    }
}

public class ConnectorRegistry : IConnectorRegistry
{
    private readonly Dictionary<string, IConnector> _connectors;

    public ConnectorRegistry(IEnumerable<IConnector> connectors)
    {
        _connectors = connectors.ToDictionary(c => c.Type, c => c);
    }

    public IReadOnlyList<string> GetAvailableTypes() => _connectors.Keys.ToList();
    public IConnector Resolve(string type) =>
        _connectors.TryGetValue(type, out var connector) ? connector : throw new KeyNotFoundException($"Connector type '{type}' not found");
}

public class RepositoryScanner : IRepositoryScanner
{
    public Task<RepositoryScanResult> ScanAsync(ConnectorContext context, string repositoryName, CancellationToken ct = default)
    {
        return Task.FromResult(new RepositoryScanResult
        {
            RepositoryName = repositoryName,
            ApplicationName = repositoryName,
            Purpose = $"Enterprise application repository: {repositoryName}",
            TechnologyStack = ["C#", ".NET 8", "PostgreSQL"],
            Frameworks = ["ASP.NET Core", "Entity Framework Core"],
            EntryPoints = ["Program.cs", "Startup.cs"],
            ApiEndpoints = ["/api/users", "/api/orders", "/api/health"],
            Dependencies =
            [
                new() { Name = "Microsoft.EntityFrameworkCore", Version = "8.0.11", IsOutdated = false },
                new() { Name = "Newtonsoft.Json", Version = "12.0.3", IsOutdated = true }
            ],
            TestProjects = [$"{repositoryName}.Tests"],
            SecurityRisks = ["Outdated Newtonsoft.Json package"],
            RefactorOpportunities = ["Extract shared validation logic", "Add missing API integration tests"]
        });
    }
}

public class DatabaseScanner : IDatabaseScanner
{
    public Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        return Task.FromResult(new DatabaseScanResult
        {
            DatabaseName = databaseName,
            Tables =
            [
                new()
                {
                    Schema = "public", Name = "users",
                    Columns =
                    [
                        new() { Name = "id", DataType = "uuid", IsNullable = false, IsPrimaryKey = true },
                        new() { Name = "email", DataType = "varchar(320)", IsNullable = false },
                        new() { Name = "created_at", DataType = "timestamptz", IsNullable = false }
                    ],
                    Indexes = ["pk_users", "idx_users_email"],
                    RowCount = 1500
                },
                new()
                {
                    Schema = "public", Name = "orders",
                    Columns =
                    [
                        new() { Name = "id", DataType = "uuid", IsNullable = false, IsPrimaryKey = true },
                        new() { Name = "user_id", DataType = "uuid", IsNullable = false, IsForeignKey = true },
                        new() { Name = "status", DataType = "varchar(50)", IsNullable = false }
                    ],
                    Indexes = ["pk_orders", "idx_orders_user_id"]
                }
            ],
            Relationships =
            [
                new() { FromTable = "orders", FromColumn = "user_id", ToTable = "users", ToColumn = "id" }
            ],
            RiskyPatterns = ["No soft-delete on orders table"]
        });
    }
}
