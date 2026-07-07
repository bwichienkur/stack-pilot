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
public record OrganizationSettingsDto(Guid Id, string Name, string Slug, string Plan, Dictionary<string, bool> FeatureFlags, string? SlackWebhookUrl = null);
public record UpdateOrganizationSettingsRequest(string? Name, Dictionary<string, bool>? FeatureFlags, string? SlackWebhookUrl = null);
public record OrganizationMemberDto(Guid UserId, string Email, string? FirstName, string? LastName, string RoleName, DateTime JoinedAt);
public record OrganizationInviteDto(Guid Id, string Email, string RoleName, DateTime ExpiresAt, DateTime? AcceptedAt, DateTime CreatedAt);
public record CreateInviteRequest(string Email, Guid RoleId);
public record AcceptInviteRequest(string Token);
public record UpdateMemberRoleRequest(Guid UserId, Guid RoleId);
public record WorkspaceDto(Guid Id, Guid OrganizationId, string Name, string Slug, string? Description, bool IsActive);
public record CreateWorkspaceRequest(string Name, string Slug, string? Description);

public record ConnectorDefinitionDto(Guid Id, string Type, string Name, string? Description, string Category, string ConfigSchema, string[] Capabilities);
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
public record ReleaseScheduleDetailDto(
    Guid Id, Guid TicketId, int TicketNumber, string TicketTitle, string TicketStatus,
    DateTime ScheduledAt, string? ReleaseWindow, string Status);
public record ScheduleReleaseRequest(DateTime ScheduledAt, string? ReleaseWindow, string? RollbackPlan, string? ChecklistJson);

public record RecommendationDto(Guid Id, string Type, string Summary, string RiskLevel, decimal? ConfidenceScore, string Status, DateTime CreatedAt);

public record DocumentationPageDto(Guid Id, string Title, string DocType, int LatestVersion, string Status);
public record DocumentationVersionDto(int Version, string ContentMd, string GeneratedBy, string Status, DateTime CreatedAt);

public record AiChatRequest(string Message, Guid? ConversationId);
public record AiChatResponse(string Reply, Guid ConversationId, string[]? Citations);
public record AiCitationDto(Guid? NodeId, string Excerpt);
public record AiRequirementsResult(string BusinessSummary, string FunctionalRequirements, string NonFunctionalRequirements, string AcceptanceCriteria, decimal RiskScore, decimal ConfidenceScore, List<AiCitationDto>? Citations = null);

public record AuditLogDto(Guid Id, string Action, string? EntityType, Guid? EntityId, Guid? UserId, string? DetailsJson, DateTime CreatedAt);
public record BuildRunDto(Guid Id, Guid? TicketId, string Status, string? Conclusion, string? LogsUrl, string? PullRequestUrl, DateTime? StartedAt, DateTime? CompletedAt);
public record ApprovalGateDto(Guid Id, string GateType, string RequiredPermission, int SortOrder, bool IsEnabled);
public record UpdateApprovalGateRequest(Guid Id, bool IsEnabled, int SortOrder);

public record TicketWorkflowDto(string CurrentStatus, IReadOnlyList<string> AllowedNextStatuses);
public record UpdateReleaseRequest(string Action);

public record DashboardStatsDto(
    int ApplicationCount, int RepositoryCount, int DatabaseCount, int OpenTickets,
    int PendingApprovals, int OpenRecommendations, decimal AverageRiskScore, int ActiveConnectors,
    int UnhealthyConnectors, int ReleasesThisWeek, int HighRiskCount, int PendingQa, int PendingUat);

public record ConnectorHealthSummaryDto(int Total, int Healthy, int Degraded, int Unhealthy, int Unknown);

public record OutboundWebhookSubscriptionDto(Guid Id, string Url, string[] Events, bool IsActive, DateTime CreatedAt);
public record CreateOutboundWebhookRequest(string Url, string[] Events);

public record AiCodeSuggestionDto(Guid AiActionId, string SuggestedCode, string Language, string Summary);

public record AiUsageWithOverageDto(
    long TokensUsedThisMonth, long MonthlyBudget, long OverageTokens, bool WithinGracePeriod, bool IsOverBudget);

public record AdminOrganizationSummaryDto(Guid Id, string Name, string Slug, string Plan, bool IsActive, int MemberCount, DateTime CreatedAt);
public record AdminOrganizationDetailDto(
    Guid Id, string Name, string Slug, string Plan, string SubscriptionStatus, bool IsActive,
    DateTime? TrialEndsAt, int MemberCount, int WorkspaceCount, int ConnectorCount, DateTime CreatedAt);
public record OverridePlanRequest(string Plan);

public record RepositoryScanDto(Guid Id, string RepositoryName, string Status, DateTime? CompletedAt);

public record PlanLimitsDto(int IncludedSeats, int MaxSeats, int MaxWorkspaces, int MaxConnectors, long MonthlyAiTokenBudget, int AuditRetentionDays, bool SamlSso, bool PrioritySupport);
public record PlanPricingDto(
    string Plan, string Name, string Tagline, decimal? MonthlyPriceUsd, decimal? AnnualPriceUsd,
    decimal AdditionalSeatPriceUsd, string BillingModel, PlanLimitsDto Limits, string[] Highlights);
public record BillingUsageDto(int SeatCount, int WorkspaceCount, int ConnectorCount, long AiTokensUsedThisMonth);
public record OrganizationBillingDto(
    string Plan, string SubscriptionStatus, DateTime? TrialEndsAt, int? TrialDaysRemaining,
    PlanLimitsDto Limits, BillingUsageDto Usage, bool StripeConfigured, string? StripeCustomerId,
    bool IsWriteBlocked, string? BlockReason, bool CanOpenCustomerPortal);
public record CreateCheckoutSessionRequest(string Plan, string BillingInterval, string SuccessUrl, string CancelUrl, string? PromotionCode = null);
public record CreatePortalSessionRequest(string ReturnUrl);
public record PortalSessionDto(string Url, bool IsMock);
public record CheckoutSessionDto(string SessionId, string Url, bool IsMock);

public record PlanEnforcementDto(
    bool IsWriteBlocked,
    string? BlockReason,
    bool SeatsAtLimit,
    bool WorkspacesAtLimit,
    bool ConnectorsAtLimit,
    bool AiTokensAtLimit);
public record DatabaseScanDto(Guid Id, string DatabaseName, string Status, DateTime? CompletedAt);
