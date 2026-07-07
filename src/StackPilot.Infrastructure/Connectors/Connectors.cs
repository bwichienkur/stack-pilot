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

public class SqlServerConnector : ConnectorBase
{
    private readonly SqlServerDatabaseScanner _scanner;

    public SqlServerConnector(SqlServerDatabaseScanner scanner) => _scanner = scanner;

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

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "master").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scanned = 0;

        foreach (var db in databases)
        {
            var result = await _scanner.ScanAsync(context, db, ct);
            if (result.Tables.Count > 0) scanned++;
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = scanned,
            Metadata = new() { ["databases"] = databases, ["syncType"] = "database" }
        };
    }
}

public class PostgreSQLConnector : ConnectorBase
{
    private readonly PostgreSqlDatabaseScanner _scanner;

    public PostgreSQLConnector(PostgreSqlDatabaseScanner scanner) => _scanner = scanner;

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

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var databases = config.GetValueOrDefault("databases", "postgres").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var scanned = 0;

        foreach (var db in databases)
        {
            var result = await _scanner.ScanAsync(context, db, ct);
            if (result.Tables.Count > 0) scanned++;
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = scanned,
            Metadata = new() { ["databases"] = databases, ["syncType"] = "database" }
        };
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

public class DatabaseScannerRouter : IDatabaseScanner
{
    private readonly PostgreSqlDatabaseScanner _postgres;
    private readonly SqlServerDatabaseScanner _sqlServer;

    public DatabaseScannerRouter(PostgreSqlDatabaseScanner postgres, SqlServerDatabaseScanner sqlServer)
    {
        _postgres = postgres;
        _sqlServer = sqlServer;
    }

    public Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default)
    {
        var scanner = context.ConnectorType switch
        {
            "postgresql" => (IDatabaseScanner)_postgres,
            "sql_server" => _sqlServer,
            _ => _postgres
        };
        return scanner.ScanAsync(context, databaseName, ct);
    }
}
