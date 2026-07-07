using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackPilot.Application.Common;
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
        using var activity = StackPilotTelemetry.StartActivity("job.connector_sync");
        activity?.SetTag("connector.id", connectorId);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IConnectorRegistry>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();
        var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var instance = await db.ConnectorInstances
            .IgnoreQueryFilters()
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.Id == connectorId, ct);

        if (instance is null) return;

        tenant.SetOrganization(instance.OrganizationId);
        await db.SetOrganizationAsync(instance.OrganizationId, ct);

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

            if (instance.Definition.Type == "jira")
                await SyncJiraTicketsAsync(db, instance, result.Metadata, ct);

            var repoNames = ExtractRepositoryNames(result.Metadata);
            foreach (var repo in repoNames)
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

                if (instance.Definition.Type is "github_repository" or "gitlab_repository")
                    jobs.EnqueueRepositoryScan(connectorId, repo);
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

    private static async Task SyncJiraTicketsAsync(AppDbContext db, ConnectorInstance instance, Dictionary<string, object>? metadata, CancellationToken ct)
    {
        if (metadata is null || !metadata.TryGetValue("issues", out var issuesObj)) return;
        if (issuesObj is not List<Dictionary<string, string>> issues) return;

        foreach (var issue in issues)
        {
            if (!issue.TryGetValue("externalId", out var externalId)) continue;
            var existing = await db.Tickets.FirstOrDefaultAsync(t =>
                t.WorkspaceId == instance.WorkspaceId && t.ExternalReference == externalId, ct);

            var title = issue.GetValueOrDefault("title") ?? externalId;
            var description = issue.GetValueOrDefault("description");
            var priority = MapJiraPriority(issue.GetValueOrDefault("priority"));
            var ticketType = MapJiraType(issue.GetValueOrDefault("ticketType"));

            if (existing is null)
            {
                db.Tickets.Add(new Ticket
                {
                    OrganizationId = instance.OrganizationId,
                    WorkspaceId = instance.WorkspaceId,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    TicketType = ticketType,
                    ExternalReference = externalId,
                    Status = TicketStatus.Submitted,
                    RequesterId = await db.OrganizationMembers
                        .Where(m => m.OrganizationId == instance.OrganizationId)
                        .Select(m => m.UserId)
                        .FirstAsync(ct)
                });
            }
            else
            {
                existing.Title = title;
                existing.Description = description;
                existing.Priority = priority;
                existing.TicketType = ticketType;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static TicketPriority MapJiraPriority(string? priority) => priority?.ToLowerInvariant() switch
    {
        "highest" or "high" => TicketPriority.High,
        "low" or "lowest" => TicketPriority.Low,
        "critical" => TicketPriority.Critical,
        _ => TicketPriority.Medium
    };

    private static TicketType MapJiraType(string? type) => type?.ToLowerInvariant() switch
    {
        "bug" => TicketType.Bug,
        "story" or "epic" => TicketType.NewFeature,
        _ => TicketType.Enhancement
    };

    private static IEnumerable<string> ExtractRepositoryNames(Dictionary<string, object>? metadata)
    {
        if (metadata is null) yield break;

        if (metadata.TryGetValue("repositories", out var repos) && repos is string[] githubRepos)
        {
            foreach (var repo in githubRepos) yield return repo;
            yield break;
        }

        if (metadata.TryGetValue("projects", out var projects) && projects is string[] gitlabProjects)
        {
            foreach (var project in gitlabProjects) yield return project;
        }
    }
}

public class RepositoryScanJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RepositoryScanJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(Guid connectorId, string repositoryName, CancellationToken ct)
    {
        using var activity = StackPilotTelemetry.StartActivity("job.repository_scan");
        activity?.SetTag("connector.id", connectorId);
        activity?.SetTag("repository", repositoryName);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IRepositoryScanner>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var instance = await db.ConnectorInstances
            .IgnoreQueryFilters()
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstAsync(c => c.Id == connectorId, ct);

        tenant.SetOrganization(instance.OrganizationId);
        await db.SetOrganizationAsync(instance.OrganizationId, ct);

        var scan = await db.RepositoryScans
            .Where(s => s.ConnectorId == connectorId && s.RepositoryName == repositoryName && s.Status == ScanStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (scan is null)
        {
            scan = new RepositoryScan
            {
                OrganizationId = instance.OrganizationId,
                ConnectorId = connectorId,
                RepositoryName = repositoryName,
                Status = ScanStatus.Pending
            };
            db.RepositoryScans.Add(scan);
            await db.SaveChangesAsync(ct);
        }

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

        var rag = scope.ServiceProvider.GetRequiredService<IRagIndexService>();
        await rag.IndexRepositoryScanAsync(instance.OrganizationId, instance.WorkspaceId, repoNode?.Id, repositoryName, scan.ResultsJson ?? "{}", ct);

        await db.SaveChangesAsync(ct);
    }
}

public class DatabaseScanJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseScanJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task ExecuteAsync(Guid connectorId, string databaseName, CancellationToken ct)
    {
        using var activity = StackPilotTelemetry.StartActivity("job.database_scan");
        activity?.SetTag("connector.id", connectorId);
        activity?.SetTag("database", databaseName);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scanner = scope.ServiceProvider.GetRequiredService<IDatabaseScanner>();
        var encryption = scope.ServiceProvider.GetRequiredService<ICredentialEncryptionService>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var instance = await db.ConnectorInstances
            .IgnoreQueryFilters()
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstAsync(c => c.Id == connectorId, ct);

        tenant.SetOrganization(instance.OrganizationId);
        await db.SetOrganizationAsync(instance.OrganizationId, ct);

        var scan = await db.DatabaseScans
            .Where(s => s.ConnectorId == connectorId && s.DatabaseName == databaseName && s.Status == ScanStatus.Pending)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (scan is null)
        {
            scan = new DatabaseScan
            {
                OrganizationId = instance.OrganizationId,
                ConnectorId = connectorId,
                DatabaseName = databaseName,
                Status = ScanStatus.Pending
            };
            db.DatabaseScans.Add(scan);
            await db.SaveChangesAsync(ct);
        }

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

        GraphNode? previousTableNode = null;
        foreach (var table in result.Tables)
        {
            var tableNode = new GraphNode
            {
                OrganizationId = instance.OrganizationId,
                WorkspaceId = instance.WorkspaceId,
                NodeType = GraphNodeType.Table,
                Name = $"{table.Schema}.{table.Name}",
                MetadataJson = JsonSerializer.Serialize(new { table.Columns.Count, table.RowCount })
            };
            db.GraphNodes.Add(tableNode);

            db.GraphEdges.Add(new GraphEdge
            {
                OrganizationId = instance.OrganizationId,
                SourceNodeId = dbNode.Id,
                TargetNodeId = tableNode.Id,
                EdgeType = GraphEdgeType.BelongsTo
            });

            previousTableNode = tableNode;
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
        using var activity = StackPilotTelemetry.StartActivity("job.generate_requirements");
        activity?.SetTag("ticket.id", ticketId);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aiService = scope.ServiceProvider.GetRequiredService<IAiService>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var ticket = await db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is not null)
        {
            tenant.SetOrganization(ticket.OrganizationId);
            await db.SetOrganizationAsync(ticket.OrganizationId, ct);
        }

        await aiService.GenerateRequirementsAsync(ticketId, ct);
    }
}

