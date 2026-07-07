namespace StackPilot.Application.DTOs;

public record LoginRequest(string Email, string Password);
public record TriggerRepositoryScanRequest(Guid ConnectorId, string RepositoryName);
public record TriggerDatabaseScanRequest(Guid ConnectorId, string DatabaseName);
public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
public record RefreshTokenRequest(string RefreshToken);

public record UserDto(Guid Id, string Email, string? FirstName, string? LastName, string? AvatarUrl);
public record OrganizationDto(Guid Id, string Name, string Slug, string Plan, bool IsActive);
public record OrganizationCreatedDto(OrganizationDto Organization, string AccessToken);
public record CreateOrganizationRequest(string Name, string Slug);
public record WorkspaceDto(Guid Id, Guid OrganizationId, string Name, string Slug, string? Description, bool IsActive);
public record CreateWorkspaceRequest(string Name, string Slug, string? Description);

public record ConnectorDefinitionDto(Guid Id, string Type, string Name, string? Description, string ConfigSchema, string[] Capabilities);
public record ConnectorInstanceDto(Guid Id, string Name, string Type, string Status, string HealthStatus, DateTime? LastSyncAt, DateTime CreatedAt);
public record CreateConnectorRequest(string Name, Guid DefinitionId, string ConfigJson, Dictionary<string, string>? Credentials);
public record SyncHistoryDto(Guid Id, string Status, DateTime StartedAt, DateTime? CompletedAt, int ItemsProcessed);

public record GraphNodeDto(Guid Id, string NodeType, string Name, string? ExternalId, decimal? RiskScore, string? MetadataJson);
public record GraphEdgeDto(Guid Id, Guid SourceNodeId, Guid TargetNodeId, string EdgeType);
public record GraphSearchRequest(string Query, string? NodeType, int Limit = 20);
public record ImpactAnalysisDto(Guid NodeId, List<GraphNodeDto> ImpactedNodes, List<GraphEdgeDto> Paths);

public record TicketDto(
    Guid Id, int TicketNumber, string Title, string? Description, string TicketType, string Status,
    string Priority, Guid RequesterId, Guid? AssigneeId, decimal? RiskScore, decimal? ConfidenceScore,
    DateTime CreatedAt, DateTime UpdatedAt);
public record CreateTicketRequest(string Title, string? Description, string TicketType, string Priority, string? BusinessJustification);
public record UpdateTicketRequest(string? Title, string? Description, string? Status, string? Priority, Guid? AssigneeId);
public record TicketDetailDto(
    Guid Id, int TicketNumber, string Title, string? Description, string TicketType, string Status,
    string Priority, Guid RequesterId, Guid? AssigneeId, decimal? RiskScore, decimal? ConfidenceScore,
    DateTime CreatedAt, DateTime UpdatedAt,
    string? BusinessJustification, string? AiRequirementsJson, string? ImplementationPlanJson,
    List<TicketCommentDto> Comments, List<ApprovalDto> Approvals);

public record TicketCommentDto(Guid Id, Guid UserId, string Content, DateTime CreatedAt);
public record AddCommentRequest(string Content);

public record ApprovalDto(Guid Id, string ApprovalType, Guid ApproverId, string Decision, string? Comments, DateTime DecidedAt);
public record SubmitApprovalRequest(string ApprovalType, string Decision, string? Comments);

public record QaEvidenceDto(Guid Id, Guid TesterId, string Result, string? Notes, DateTime CreatedAt);
public record SubmitQaRequest(string Result, string? Notes, string[]? EvidenceUrls);

public record UatDecisionDto(Guid Id, Guid ApproverId, string Decision, string? Comments, DateTime DecidedAt);
public record SubmitUatRequest(string Decision, string? Comments);

public record ReleaseScheduleDto(Guid Id, Guid TicketId, DateTime ScheduledAt, string? ReleaseWindow, string Status);
public record ScheduleReleaseRequest(DateTime ScheduledAt, string? ReleaseWindow, string? RollbackPlan, string? ChecklistJson);

public record RecommendationDto(Guid Id, string Type, string Summary, string RiskLevel, decimal? ConfidenceScore, string Status, DateTime CreatedAt);

public record DocumentationPageDto(Guid Id, string Title, string DocType, int LatestVersion, string Status);
public record DocumentationVersionDto(int Version, string ContentMd, string GeneratedBy, string Status, DateTime CreatedAt);

public record AiChatRequest(string Message, Guid? ConversationId);
public record AiChatResponse(string Reply, Guid ConversationId, string[]? Citations);
public record AiCitationDto(Guid? NodeId, string Excerpt);
public record AiRequirementsResult(string BusinessSummary, string FunctionalRequirements, string NonFunctionalRequirements, string AcceptanceCriteria, decimal RiskScore, decimal ConfidenceScore, List<AiCitationDto>? Citations = null);

public record AuditLogDto(Guid Id, string Action, string? EntityType, Guid? EntityId, Guid? UserId, DateTime CreatedAt);
public record BuildRunDto(Guid Id, string Status, string? Conclusion, string? LogsUrl, string? PullRequestUrl, DateTime? StartedAt, DateTime? CompletedAt);

public record DashboardStatsDto(
    int ApplicationCount, int RepositoryCount, int DatabaseCount, int OpenTickets,
    int PendingApprovals, int OpenRecommendations, decimal AverageRiskScore, int ActiveConnectors);

public record RepositoryScanDto(Guid Id, string RepositoryName, string Status, DateTime? CompletedAt);
public record DatabaseScanDto(Guid Id, string DatabaseName, string Status, DateTime? CompletedAt);
