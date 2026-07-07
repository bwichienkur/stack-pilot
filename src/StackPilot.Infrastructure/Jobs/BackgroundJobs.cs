using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackPilot.Application.Connectors;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Jobs;

public class BackgroundJobService : IBackgroundJobService
{
    public string EnqueueConnectorSync(Guid connectorId) =>
        BackgroundJob.Enqueue<ConnectorSyncJob>(j => j.ExecuteAsync(connectorId, CancellationToken.None));

    public string EnqueueRepositoryScan(Guid connectorId, string repositoryName) =>
        BackgroundJob.Enqueue<RepositoryScanJob>(j => j.ExecuteAsync(connectorId, repositoryName, CancellationToken.None));

    public string EnqueueDatabaseScan(Guid connectorId, string databaseName) =>
        BackgroundJob.Enqueue<DatabaseScanJob>(j => j.ExecuteAsync(connectorId, databaseName, CancellationToken.None));

    public string EnqueueGenerateRequirements(Guid ticketId) =>
        BackgroundJob.Enqueue<GenerateRequirementsJob>(j => j.ExecuteAsync(ticketId, CancellationToken.None));
}

public class ConnectorSyncJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectorSyncJob> _logger;

    public ConnectorSyncJob(IServiceScopeFactory scopeFactory, ILogger<ConnectorSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid connectorId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IConnectorRegistry>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();

        var instance = await db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.Id == connectorId, ct);

        if (instance is null) return;

        var history = new SyncHistory { ConnectorId = connectorId, Status = SyncStatus.Running, StartedAt = DateTime.UtcNow };
        db.SyncHistories.Add(history);
        await db.SaveChangesAsync(ct);

        try
        {
            var credentials = instance.Credentials.ToDictionary(
                c => c.CredentialType,
                c => encryption.Decrypt(c.EncryptedValue, instance.OrganizationId));

            var context = new ConnectorContext
            {
                OrganizationId = instance.OrganizationId,
                WorkspaceId = instance.WorkspaceId,
                ConnectorInstanceId = instance.Id,
                ConnectorType = instance.Definition.Type,
                ConfigJson = instance.ConfigJson,
                Credentials = credentials
            };

            var connector = registry.Resolve(instance.Definition.Type);
            var result = await connector.SyncAsync(context, ct);

            history.Status = result.Success ? SyncStatus.Completed : SyncStatus.Failed;
            history.ItemsProcessed = result.ItemsProcessed;
            history.CompletedAt = DateTime.UtcNow;
            history.ErrorsJson = result.Errors.Count > 0 ? JsonSerializer.Serialize(result.Errors) : null;

            instance.LastSyncAt = DateTime.UtcNow;
            instance.Status = ConnectorStatus.Active;
            instance.HealthStatus = HealthStatus.Healthy;

            if (result.Metadata?.TryGetValue("repositories", out var repos) == true && repos is string[] repoList)
            {
                foreach (var repo in repoList)
                {
                    var existing = await db.GraphNodes.FirstOrDefaultAsync(n =>
                        n.WorkspaceId == instance.WorkspaceId && n.NodeType == GraphNodeType.Repository && n.Name == repo, ct);

                    if (existing is null)
                    {
                        db.GraphNodes.Add(new GraphNode
                        {
                            OrganizationId = instance.OrganizationId,
                            WorkspaceId = instance.WorkspaceId,
                            NodeType = GraphNodeType.Repository,
                            Name = repo,
                            ExternalId = $"{instance.Definition.Type}:{repo}"
                        });
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Connector sync completed for {ConnectorId}: {Items} items", connectorId, result.ItemsProcessed);
        }
        catch (Exception ex)
        {
            history.Status = SyncStatus.Failed;
            history.CompletedAt = DateTime.UtcNow;
            history.ErrorsJson = JsonSerializer.Serialize(new[] { ex.Message });
            instance.HealthStatus = HealthStatus.Unhealthy;
            await db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Connector sync failed for {ConnectorId}", connectorId);
            throw;
        }
    }
}

public class RepositoryScanJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RepositoryScanJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(Guid connectorId, string repositoryName, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IRepositoryScanner>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();

        var scan = await db.RepositoryScans
            .Where(s => s.ConnectorId == connectorId && s.RepositoryName == repositoryName && s.Status == ScanStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (scan is null) return;

        var instance = await db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstAsync(c => c.Id == connectorId, ct);
        scan.Status = ScanStatus.Running;
        scan.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var context = new ConnectorContext
        {
            OrganizationId = instance.OrganizationId,
            WorkspaceId = instance.WorkspaceId,
            ConnectorInstanceId = instance.Id,
            ConnectorType = instance.Definition.Type,
            ConfigJson = instance.ConfigJson,
            Credentials = instance.Credentials.ToDictionary(c => c.CredentialType, c => encryption.Decrypt(c.EncryptedValue, instance.OrganizationId))
        };

        var result = await scanner.ScanAsync(context, repositoryName, ct);
        scan.ResultsJson = JsonSerializer.Serialize(result);
        scan.Status = ScanStatus.Completed;
        scan.CompletedAt = DateTime.UtcNow;

        var appNode = new GraphNode
        {
            OrganizationId = instance.OrganizationId,
            WorkspaceId = instance.WorkspaceId,
            NodeType = GraphNodeType.Application,
            Name = result.ApplicationName ?? repositoryName,
            MetadataJson = JsonSerializer.Serialize(new { result.TechnologyStack, result.Frameworks }),
            RiskScore = result.SecurityRisks.Count * 2.5m
        };
        db.GraphNodes.Add(appNode);

        var repoNode = await db.GraphNodes.FirstOrDefaultAsync(n =>
            n.WorkspaceId == instance.WorkspaceId && n.NodeType == GraphNodeType.Repository && n.Name == repositoryName, ct);

        if (repoNode is not null)
        {
            db.GraphEdges.Add(new GraphEdge
            {
                OrganizationId = instance.OrganizationId,
                SourceNodeId = appNode.Id,
                TargetNodeId = repoNode.Id,
                EdgeType = GraphEdgeType.Owns
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

public class DatabaseScanJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseScanJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(Guid connectorId, string databaseName, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IDatabaseScanner>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();

        var scan = await db.DatabaseScans
            .Where(s => s.ConnectorId == connectorId && s.DatabaseName == databaseName && s.Status == ScanStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (scan is null) return;

        var instance = await db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstAsync(c => c.Id == connectorId, ct);
        scan.Status = ScanStatus.Running;
        scan.StartedAt = DateTime.UtcNow;

        var context = new ConnectorContext
        {
            OrganizationId = instance.OrganizationId,
            WorkspaceId = instance.WorkspaceId,
            ConnectorInstanceId = instance.Id,
            ConnectorType = instance.Definition.Type,
            ConfigJson = instance.ConfigJson,
            Credentials = instance.Credentials.ToDictionary(c => c.CredentialType, c => encryption.Decrypt(c.EncryptedValue, instance.OrganizationId))
        };

        var result = await scanner.ScanAsync(context, databaseName, ct);
        scan.ResultsJson = JsonSerializer.Serialize(result);
        scan.Status = ScanStatus.Completed;
        scan.CompletedAt = DateTime.UtcNow;

        var dbNode = new GraphNode
        {
            OrganizationId = instance.OrganizationId,
            WorkspaceId = instance.WorkspaceId,
            NodeType = GraphNodeType.Database,
            Name = databaseName,
            MetadataJson = JsonSerializer.Serialize(new { tableCount = result.Tables.Count })
        };
        db.GraphNodes.Add(dbNode);

        foreach (var table in result.Tables)
        {
            db.GraphNodes.Add(new GraphNode
            {
                OrganizationId = instance.OrganizationId,
                WorkspaceId = instance.WorkspaceId,
                NodeType = GraphNodeType.Table,
                Name = $"{table.Schema}.{table.Name}",
                MetadataJson = JsonSerializer.Serialize(new { table.Columns.Count, table.RowCount })
            });
        }

        await db.SaveChangesAsync(ct);
    }
}

public class GenerateRequirementsJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GenerateRequirementsJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(Guid ticketId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
        await aiService.GenerateRequirementsAsync(ticketId, ct);
    }
}
