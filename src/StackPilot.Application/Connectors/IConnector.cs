namespace StackPilot.Application.Connectors;

[Flags]
public enum ConnectorCapabilities
{
    None = 0,
    RepositoryScan = 1,
    DatabaseScan = 2,
    CiCdTracking = 4,
    WebhookReceive = 8,
    CodeIndexing = 16,
    DeploymentTracking = 32
}

public class ConnectorContext
{
    public Guid OrganizationId { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid ConnectorInstanceId { get; init; }
    public string ConfigJson { get; init; } = "{}";
    public IReadOnlyDictionary<string, string> Credentials { get; init; } = new Dictionary<string, string>();
}

public class ConnectionTestResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object>? Details { get; init; }
}

public class SyncResult
{
    public bool Success { get; init; }
    public int ItemsProcessed { get; init; }
    public List<string> Errors { get; init; } = [];
    public Dictionary<string, object>? Metadata { get; init; }
}

public class HealthCheckResult
{
    public bool IsHealthy { get; init; }
    public string Status { get; init; } = "unknown";
    public string? Message { get; init; }
}

public interface IConnector
{
    string Type { get; }
    ConnectorCapabilities Capabilities { get; }
    Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default);
    Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default);
    Task<HealthCheckResult> HealthCheckAsync(ConnectorContext context, CancellationToken ct = default);
}

public interface IConnectorRegistry
{
    IReadOnlyList<string> GetAvailableTypes();
    IConnector Resolve(string type);
}

public interface IRepositoryScanner
{
    Task<RepositoryScanResult> ScanAsync(ConnectorContext context, string repositoryName, CancellationToken ct = default);
}

public interface IDatabaseScanner
{
    Task<DatabaseScanResult> ScanAsync(ConnectorContext context, string databaseName, CancellationToken ct = default);
}

public class RepositoryScanResult
{
    public string RepositoryName { get; set; } = string.Empty;
    public string? ApplicationName { get; set; }
    public string? Purpose { get; set; }
    public List<string> TechnologyStack { get; set; } = [];
    public List<string> Frameworks { get; set; } = [];
    public List<string> EntryPoints { get; set; } = [];
    public List<string> ApiEndpoints { get; set; } = [];
    public List<DependencyInfo> Dependencies { get; set; } = [];
    public List<string> TestProjects { get; set; } = [];
    public List<string> SecurityRisks { get; set; } = [];
    public List<string> RefactorOpportunities { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class DependencyInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsOutdated { get; set; }
}

public class DatabaseScanResult
{
    public string DatabaseName { get; set; } = string.Empty;
    public List<TableInfo> Tables { get; set; } = [];
    public List<ViewInfo> Views { get; set; } = [];
    public List<StoredProcedureInfo> StoredProcedures { get; set; } = [];
    public List<RelationshipInfo> Relationships { get; set; } = [];
    public List<string> RiskyPatterns { get; set; } = [];
}

public class TableInfo
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = [];
    public List<string> Indexes { get; set; } = [];
    public long? RowCount { get; set; }
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

public class ViewInfo
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = string.Empty;
}

public class StoredProcedureInfo
{
    public string Schema { get; set; } = "dbo";
    public string Name { get; set; } = string.Empty;
}

public class RelationshipInfo
{
    public string FromTable { get; set; } = string.Empty;
    public string FromColumn { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
}
