using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Caching;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class GraphService : IGraphService
{
    private readonly AppDbContext _db;

    public GraphService(AppDbContext db) => _db = db;

    public async Task<PagedResult<GraphNodeDto>> GetNodesAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.GraphNodes.Where(n => n.WorkspaceId == workspaceId);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(n => n.Name.Contains(request.Search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(n => n.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => MapNode(n))
            .ToListAsync(ct);

        return new PagedResult<GraphNodeDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    public async Task<GraphNodeDto?> GetNodeAsync(Guid nodeId, CancellationToken ct = default)
    {
        var node = await _db.GraphNodes.FindAsync([nodeId], ct);
        return node is null ? null : MapNode(node);
    }

    public async Task<List<GraphEdgeDto>> GetEdgesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await _db.GraphEdges
            .Where(e => e.SourceNode!.WorkspaceId == workspaceId)
            .Select(e => new GraphEdgeDto(e.Id, e.SourceNodeId, e.TargetNodeId, e.EdgeType.ToString()))
            .ToListAsync(ct);
    }

    public async Task<List<GraphNodeDto>> SearchAsync(Guid workspaceId, GraphSearchRequest request, CancellationToken ct = default)
    {
        var query = _db.GraphNodes.Where(n => n.WorkspaceId == workspaceId && n.Name.Contains(request.Query));
        if (!string.IsNullOrWhiteSpace(request.NodeType))
            query = query.Where(n => n.NodeType == Enum.Parse<GraphNodeType>(request.NodeType, true));

        return await query.Take(request.Limit).Select(n => MapNode(n)).ToListAsync(ct);
    }

    public async Task<ImpactAnalysisDto> AnalyzeImpactAsync(Guid nodeId, CancellationToken ct = default)
    {
        var visited = new HashSet<Guid>();
        var impacted = new List<GraphNodeDto>();
        var paths = new List<GraphEdgeDto>();
        await TraverseAsync(nodeId, visited, impacted, paths, ct);
        return new ImpactAnalysisDto(nodeId, impacted, paths);
    }

    private async Task TraverseAsync(Guid nodeId, HashSet<Guid> visited, List<GraphNodeDto> nodes, List<GraphEdgeDto> edges, CancellationToken ct)
    {
        if (!visited.Add(nodeId)) return;

        var outgoing = await _db.GraphEdges
            .Include(e => e.TargetNode)
            .Where(e => e.SourceNodeId == nodeId)
            .ToListAsync(ct);

        foreach (var edge in outgoing)
        {
            edges.Add(new GraphEdgeDto(edge.Id, edge.SourceNodeId, edge.TargetNodeId, edge.EdgeType.ToString()));
            if (edge.TargetNode is not null)
            {
                nodes.Add(MapNode(edge.TargetNode));
                await TraverseAsync(edge.TargetNodeId, visited, nodes, edges, ct);
            }
        }
    }

    private static GraphNodeDto MapNode(GraphNode n) =>
        new(n.Id, n.NodeType.ToString(), n.Name, n.ExternalId, n.RiskScore, n.MetadataJson);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public DashboardService(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.DashboardStats(workspaceId);
        var cached = await _cache.GetAsync<DashboardStatsDto>(cacheKey, ct);
        if (cached is not null) return cached;

        var appCount = await _db.GraphNodes.CountAsync(n => n.WorkspaceId == workspaceId && n.NodeType == GraphNodeType.Application, ct);
        var repoCount = await _db.GraphNodes.CountAsync(n => n.WorkspaceId == workspaceId && n.NodeType == GraphNodeType.Repository, ct);
        var dbCount = await _db.GraphNodes.CountAsync(n => n.WorkspaceId == workspaceId && n.NodeType == GraphNodeType.Database, ct);
        var openTickets = await _db.Tickets.CountAsync(t => t.WorkspaceId == workspaceId && t.Status != TicketStatus.Closed, ct);
        var pendingApprovals = await _db.Tickets.CountAsync(t => t.WorkspaceId == workspaceId && t.Status == TicketStatus.AwaitingApproval, ct);
        var recommendations = await _db.Recommendations.CountAsync(r => r.WorkspaceId == workspaceId && r.Status == RecommendationStatus.Open, ct);
        var avgRisk = await _db.GraphNodes.Where(n => n.WorkspaceId == workspaceId && n.RiskScore != null).AverageAsync(n => (decimal?)n.RiskScore, ct) ?? 0;
        var connectors = await _db.ConnectorInstances.CountAsync(c => c.WorkspaceId == workspaceId && c.Status == ConnectorStatus.Active, ct);

        var stats = new DashboardStatsDto(appCount, repoCount, dbCount, openTickets, pendingApprovals, recommendations, avgRisk, connectors);
        await _cache.SetAsync(cacheKey, stats, TimeSpan.FromSeconds(60), ct);
        return stats;
    }
}

public class RecommendationService : IRecommendationService
{
    private readonly AppDbContext _db;

    public RecommendationService(AppDbContext db) => _db = db;

    public async Task<PagedResult<RecommendationDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Recommendations.Where(r => r.WorkspaceId == workspaceId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RecommendationDto(r.Id, r.Type.ToString(), r.Summary, r.RiskLevel.ToString(), r.ConfidenceScore, r.Status.ToString(), r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<RecommendationDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }
}

public class DocumentationService : IDocumentationService
{
    private readonly AppDbContext _db;

    public DocumentationService(AppDbContext db) => _db = db;

    public async Task<PagedResult<DocumentationPageDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.DocumentationPages.Where(d => d.WorkspaceId == workspaceId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DocumentationPageDto(d.Id, d.Title, d.DocType,
                d.Versions.OrderByDescending(v => v.Version).Select(v => v.Version).FirstOrDefault(),
                d.Versions.OrderByDescending(v => v.Version).Select(v => v.Status.ToString()).FirstOrDefault() ?? "Draft"))
            .ToListAsync(ct);

        return new PagedResult<DocumentationPageDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    public async Task<DocumentationVersionDto?> GetLatestVersionAsync(Guid pageId, CancellationToken ct = default)
    {
        var version = await _db.DocumentationVersions
            .Where(v => v.PageId == pageId)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);

        return version is null ? null : new DocumentationVersionDto(version.Version, version.ContentMd, version.GeneratedBy, version.Status.ToString(), version.CreatedAt);
    }
}

public class IntelligenceService : IIntelligenceService
{
    private readonly AppDbContext _db;
    private readonly IBackgroundJobService _jobs;

    public IntelligenceService(AppDbContext db, IBackgroundJobService jobs)
    {
        _db = db;
        _jobs = jobs;
    }

    public async Task<List<RepositoryScanDto>> GetRepositoryScansAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var connectorIds = await _db.ConnectorInstances
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => c.Id).ToListAsync(ct);

        return await _db.RepositoryScans
            .Where(s => connectorIds.Contains(s.ConnectorId))
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new RepositoryScanDto(s.Id, s.RepositoryName, s.Status.ToString(), s.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<List<DatabaseScanDto>> GetDatabaseScansAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var connectorIds = await _db.ConnectorInstances
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => c.Id).ToListAsync(ct);

        return await _db.DatabaseScans
            .Where(s => connectorIds.Contains(s.ConnectorId))
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new DatabaseScanDto(s.Id, s.DatabaseName, s.Status.ToString(), s.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<RepositoryScanDto> TriggerRepositoryScanAsync(Guid connectorId, string repositoryName, CancellationToken ct = default)
    {
        var connector = await _db.ConnectorInstances.FindAsync([connectorId], ct)
            ?? throw new KeyNotFoundException("Connector not found");

        var scan = new RepositoryScan
        {
            OrganizationId = connector.OrganizationId,
            ConnectorId = connectorId,
            RepositoryName = repositoryName,
            Status = ScanStatus.Pending
        };
        _db.RepositoryScans.Add(scan);
        await _db.SaveChangesAsync(ct);
        _jobs.EnqueueRepositoryScan(connectorId, repositoryName);
        return new RepositoryScanDto(scan.Id, scan.RepositoryName, scan.Status.ToString(), null);
    }

    public async Task<DatabaseScanDto> TriggerDatabaseScanAsync(Guid connectorId, string databaseName, CancellationToken ct = default)
    {
        var connector = await _db.ConnectorInstances.FindAsync([connectorId], ct)
            ?? throw new KeyNotFoundException("Connector not found");

        var scan = new DatabaseScan
        {
            OrganizationId = connector.OrganizationId,
            ConnectorId = connectorId,
            DatabaseName = databaseName,
            Status = ScanStatus.Pending
        };
        _db.DatabaseScans.Add(scan);
        await _db.SaveChangesAsync(ct);
        _jobs.EnqueueDatabaseScan(connectorId, databaseName);
        return new DatabaseScanDto(scan.Id, scan.DatabaseName, scan.Status.ToString(), null);
    }

    public async Task<List<BuildRunDto>> GetBuildRunsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var connectorIds = await _db.ConnectorInstances
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => c.Id).ToListAsync(ct);

        return await _db.BuildRuns
            .Where(b => b.ConnectorId != null && connectorIds.Contains(b.ConnectorId.Value))
            .OrderByDescending(b => b.StartedAt ?? b.CreatedAt)
            .Take(50)
            .Select(b => new BuildRunDto(b.Id, b.Status.ToString(), b.Conclusion, b.LogsUrl, b.PullRequestUrl, b.StartedAt, b.CompletedAt))
            .ToListAsync(ct);
    }
}
