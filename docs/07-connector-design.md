# StackPilot — Connector Design

## 1. Connector Interface

```csharp
public interface IConnector
{
    string Type { get; }
    ConnectorCapabilities Capabilities { get; }
    Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct);
    Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct);
    Task<HealthCheckResult> HealthCheckAsync(ConnectorContext context, CancellationToken ct);
}

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
```

## 2. Connector Context

```csharp
public class ConnectorContext
{
    public Guid OrganizationId { get; init; }
    public Guid WorkspaceId { get; init; }
    public Guid ConnectorInstanceId { get; init; }
    public JsonDocument Config { get; init; }
    public IReadOnlyDictionary<string, string> DecryptedCredentials { get; init; }
    public IConnectorLogger Logger { get; init; }
}
```

## 3. Credential Management

- Credentials encrypted with AES-256-GCM
- Per-organization key derived from master key + org ID (HKDF)
- Key rotation support via `key_version` field
- Credentials never logged or returned in API responses
- Supported credential types: `oauth_token`, `pat`, `connection_string`, `api_key`, `certificate`

## 4. MVP Connectors

### GitHub Repository Connector

| Field | Value |
|-------|-------|
| Type | `github_repository` |
| Capabilities | RepositoryScan, CodeIndexing, WebhookReceive |
| Auth | GitHub PAT or OAuth App |
| Config | `owner`, `repositories[]`, `branch` |
| Sync | Clone/fetch repos, scan file structure, detect stack |

### SQL Server Connector

| Field | Value |
|-------|-------|
| Type | `sql_server` |
| Capabilities | DatabaseScan |
| Auth | Connection string (encrypted) |
| Config | `server`, `databases[]` |
| Sync | Schema discovery via INFORMATION_SCHEMA |

### PostgreSQL Connector

| Field | Value |
|-------|-------|
| Type | `postgresql` |
| Capabilities | DatabaseScan |
| Auth | Connection string (encrypted) |
| Config | `host`, `port`, `databases[]` |
| Sync | Schema discovery via pg_catalog |

### GitHub Actions Connector

| Field | Value |
|-------|-------|
| Type | `github_actions` |
| Capabilities | CiCdTracking, WebhookReceive, DeploymentTracking |
| Auth | GitHub PAT or OAuth App |
| Config | `owner`, `repositories[]` |
| Sync | Fetch workflow runs, build results, deployment status |

## 5. Connector Registry

```csharp
public interface IConnectorRegistry
{
    IReadOnlyList<ConnectorDefinition> GetDefinitions();
    IConnector Resolve(string type);
    void Register(string type, Func<IServiceProvider, IConnector> factory);
}
```

Connectors registered at startup via DI:

```csharp
services.AddConnector<GitHubRepositoryConnector>("github_repository");
services.AddConnector<SqlServerConnector>("sql_server");
services.AddConnector<PostgreSQLConnector>("postgresql");
services.AddConnector<GitHubActionsConnector>("github_actions");
```

## 6. Sync Pipeline

```
Trigger (manual / scheduled / webhook)
        ↓
Create SyncHistory record (status: running)
        ↓
Decrypt credentials
        ↓
Connector.SyncAsync()
  ├── Health check
  ├── Fetch data
  ├── Transform to domain models
  └── Update knowledge graph nodes/edges
        ↓
Update SyncHistory (status: completed/failed)
        ↓
Emit domain event: SyncCompleted
        ↓
Queue follow-up jobs (indexing, doc generation, recommendations)
```

## 7. Error Handling

| Error Type | Handling |
|------------|----------|
| Auth failure | Mark connector unhealthy, notify admin |
| Rate limit | Exponential backoff, respect Retry-After |
| Network timeout | Retry up to 3 times |
| Partial sync | Record partial results, log errors |
| Invalid config | Validation error, block sync |

## 8. Future Connector Roadmap

| Connector | Priority | Capabilities |
|-----------|----------|-------------|
| GitLab | P1 | RepositoryScan, CiCdTracking |
| Azure DevOps | P1 | RepositoryScan, CiCdTracking |
| Jira | P1 | TicketSync |
| AWS | P2 | CloudResourceScan |
| Azure | P2 | CloudResourceScan |
| Jenkins | P2 | CiCdTracking |
| Slack | P2 | Notifications |
| Datadog | P3 | Monitoring |
| ServiceNow | P3 | TicketSync |

## 9. Connector Marketplace (Future)

- Connector definitions published with metadata, icons, config schemas
- Third-party connectors via plugin SDK
- Sandboxed execution for untrusted connectors
