using StackPilot.Domain.Common;
using StackPilot.Domain.Enums;

namespace StackPilot.Domain.Entities;

public class GraphNode : BaseEntity, ITenantEntity, IWorkspaceScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public GraphNodeType NodeType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? MetadataJson { get; set; }
    public decimal? RiskScore { get; set; }

    public ICollection<GraphEdge> OutgoingEdges { get; set; } = [];
    public ICollection<GraphEdge> IncomingEdges { get; set; } = [];
}

public class GraphEdge : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public GraphEdgeType EdgeType { get; set; }
    public string? MetadataJson { get; set; }

    public GraphNode SourceNode { get; set; } = null!;
    public GraphNode TargetNode { get; set; } = null!;
}

public class RepositoryScan : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ConnectorId { get; set; }
    public string RepositoryName { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public string? ResultsJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ConnectorInstance Connector { get; set; } = null!;
}

public class DatabaseScan : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid ConnectorId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public string? ResultsJson { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ConnectorInstance Connector { get; set; } = null!;
}

public class DocumentationPage : BaseEntity, ITenantEntity, IWorkspaceScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public Guid? GraphNodeId { get; set; }

    public GraphNode? GraphNode { get; set; }
    public ICollection<DocumentationVersion> Versions { get; set; } = [];
}

public class DocumentationVersion : BaseEntity
{
    public Guid PageId { get; set; }
    public int Version { get; set; }
    public string ContentMd { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = "human";
    public DocumentationStatus Status { get; set; } = DocumentationStatus.Draft;
    public Guid? CreatedByUserId { get; set; }

    public DocumentationPage Page { get; set; } = null!;
}

public class Recommendation : BaseEntity, ITenantEntity, IWorkspaceScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public RecommendationType Type { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? AffectedEntitiesJson { get; set; }
    public string? ImplementationPlan { get; set; }
    public string? RollbackPlan { get; set; }
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Open;
}

public class GraphChunk : BaseEntity, ITenantEntity, IWorkspaceScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? GraphNodeId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public string EmbeddingJson { get; set; } = "[]";

    public GraphNode? GraphNode { get; set; }
}
