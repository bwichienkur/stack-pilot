using StackPilot.Application.Common;
using StackPilot.Application.DTOs;

namespace StackPilot.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    Task<AuthResponse> HandleSsoLoginAsync(string email, string? firstName, string? lastName, string externalId, string provider, CancellationToken ct = default);
    Task<AuthResponse> RefreshSessionAsync(Guid userId, CancellationToken ct = default);
    Task<AuthResponse> RefreshWithTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}

public interface IOrganizationService
{
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken ct = default);
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default);
    Task<OrganizationDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<WorkspaceDto> CreateWorkspaceAsync(Guid orgId, CreateWorkspaceRequest request, CancellationToken ct = default);
    Task<List<WorkspaceDto>> GetWorkspacesAsync(Guid orgId, CancellationToken ct = default);
    Task<OrganizationSettingsDto> GetSettingsAsync(Guid orgId, CancellationToken ct = default);
    Task<OrganizationSettingsDto> UpdateSettingsAsync(Guid orgId, UpdateOrganizationSettingsRequest request, CancellationToken ct = default);
    Task<List<OrganizationMemberDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<OrganizationInviteCreatedDto> CreateInviteAsync(Guid orgId, CreateInviteRequest request, Guid invitedByUserId, CancellationToken ct = default);
    Task<List<OrganizationInviteDto>> GetInvitesAsync(Guid orgId, CancellationToken ct = default);
    Task<List<RoleDto>> GetInvitableRolesAsync(CancellationToken ct = default);
    Task RevokeInviteAsync(Guid orgId, Guid inviteId, CancellationToken ct = default);
    Task<OrganizationDto> AcceptInviteAsync(AcceptInviteRequest request, Guid userId, CancellationToken ct = default);
    Task<OrganizationMemberDto> UpdateMemberRoleAsync(Guid orgId, UpdateMemberRoleRequest request, CancellationToken ct = default);
    Task<OrganizationSamlConfigDto> GetSamlConfigAsync(Guid orgId, CancellationToken ct = default);
    Task<OrganizationSamlConfigDto> UpdateSamlConfigAsync(Guid orgId, UpdateOrganizationSamlConfigRequest request, CancellationToken ct = default);
}

public interface IConnectorService
{
    Task<List<ConnectorDefinitionDto>> GetDefinitionsAsync(CancellationToken ct = default);
    Task<List<ConnectorInstanceDto>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
    Task<ConnectorInstanceDto> CreateAsync(Guid workspaceId, CreateConnectorRequest request, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(Guid connectorId, CancellationToken ct = default);
    Task<SyncHistoryDto> TriggerSyncAsync(Guid connectorId, CancellationToken ct = default);
    Task<List<SyncHistoryDto>> GetSyncHistoryAsync(Guid connectorId, CancellationToken ct = default);
    Task<ConnectorHealthSummaryDto> GetHealthSummaryAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface IGraphService
{
    Task<PagedResult<GraphNodeDto>> GetNodesAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default);
    Task<GraphNodeDto?> GetNodeAsync(Guid nodeId, CancellationToken ct = default);
    Task<List<GraphEdgeDto>> GetEdgesAsync(Guid workspaceId, CancellationToken ct = default);
    Task<List<GraphNodeDto>> SearchAsync(Guid workspaceId, GraphSearchRequest request, CancellationToken ct = default);
    Task<ImpactAnalysisDto> AnalyzeImpactAsync(Guid nodeId, CancellationToken ct = default);
    Task<List<GraphNodeDto>> GetApplicationsAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface ITicketService
{
    Task<PagedResult<TicketDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default);
    Task<TicketDetailDto?> GetByIdAsync(Guid ticketId, CancellationToken ct = default);
    Task<TicketDto> CreateAsync(Guid workspaceId, CreateTicketRequest request, Guid requesterId, CancellationToken ct = default);
    Task<TicketDto?> UpdateAsync(Guid ticketId, UpdateTicketRequest request, CancellationToken ct = default);
    Task<TicketCommentDto> AddCommentAsync(Guid ticketId, AddCommentRequest request, Guid userId, CancellationToken ct = default);
    Task<ApprovalDto> SubmitApprovalAsync(Guid ticketId, SubmitApprovalRequest request, Guid approverId, CancellationToken ct = default);
    Task<List<TicketDto>> GetPendingApprovalsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<List<TicketDto>> GetPendingQaAsync(Guid workspaceId, CancellationToken ct = default);
    Task<List<TicketDto>> GetPendingUatAsync(Guid workspaceId, CancellationToken ct = default);
    Task<QaEvidenceDto> SubmitQaAsync(Guid ticketId, SubmitQaRequest request, Guid testerId, CancellationToken ct = default);
    Task<UatDecisionDto> SubmitUatAsync(Guid ticketId, SubmitUatRequest request, Guid approverId, CancellationToken ct = default);
    Task<ReleaseScheduleDto> ScheduleReleaseAsync(Guid ticketId, ScheduleReleaseRequest request, Guid userId, CancellationToken ct = default);
    Task<List<ReleaseScheduleDetailDto>> GetScheduledReleasesAsync(Guid workspaceId, CancellationToken ct = default);
    Task<TicketWorkflowDto> GetWorkflowAsync(Guid ticketId, CancellationToken ct = default);
    Task<ReleaseScheduleDto> UpdateReleaseAsync(Guid ticketId, Guid releaseId, UpdateReleaseRequest request, Guid userId, CancellationToken ct = default);
}

public interface IRecommendationService
{
    Task<PagedResult<RecommendationDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default);
    Task<List<RecommendationDto>> GenerateAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyAsync(string eventType, string message, Guid? organizationId = null, CancellationToken ct = default);
}

public interface IDocumentationService
{
    Task<PagedResult<DocumentationPageDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default);
    Task<DocumentationVersionDto?> GetLatestVersionAsync(Guid pageId, CancellationToken ct = default);
}

public interface IAuditService
{
    Task LogAsync(string action, string? entityType = null, Guid? entityId = null, string? details = null, CancellationToken ct = default);
    Task<PagedResult<AuditLogDto>> GetLogsAsync(Guid orgId, PagedRequest request, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(Guid workspaceId, CancellationToken ct = default);
}

public interface IIntelligenceService
{
    Task<List<RepositoryScanDto>> GetRepositoryScansAsync(Guid workspaceId, CancellationToken ct = default);
    Task<List<DatabaseScanDto>> GetDatabaseScansAsync(Guid workspaceId, CancellationToken ct = default);
    Task<RepositoryScanDto> TriggerRepositoryScanAsync(Guid connectorId, string repositoryName, CancellationToken ct = default);
    Task<DatabaseScanDto> TriggerDatabaseScanAsync(Guid connectorId, string databaseName, CancellationToken ct = default);
    Task<List<BuildRunDto>> GetBuildRunsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<List<BuildRunDto>> GetBuildRunsByTicketAsync(Guid ticketId, CancellationToken ct = default);
}

public interface IWebhookService
{
    Task HandleGitHubEventAsync(string eventType, string payloadJson, CancellationToken ct = default);
}

public interface IAiService
{
    Task<AiChatResponse> ChatAsync(Guid workspaceId, AiChatRequest request, Guid userId, CancellationToken ct = default);
    Task<AiRequirementsResult> GenerateRequirementsAsync(Guid ticketId, CancellationToken ct = default);
    Task<string> GenerateImplementationPlanAsync(Guid ticketId, CancellationToken ct = default);
    Task<string> GenerateDocumentationAsync(Guid pageId, CancellationToken ct = default);
    Task<AiCodeSuggestionDto> GenerateCodeAsync(Guid ticketId, CancellationToken ct = default);
}

public interface IAiWorkflowService
{
    Task<AiWorkflowActionResultDto> ExecuteAsync(Guid ticketId, string actionType, ExecuteAiWorkflowActionRequest request, CancellationToken ct = default);
    Task<AiActionReversalResultDto> ReverseActionAsync(Guid actionId, CancellationToken ct = default);
}

public interface ICredentialEncryptionService
{
    byte[] Encrypt(string plaintext, Guid organizationId);
    string Decrypt(byte[] ciphertext, Guid organizationId);
}

public interface IBackgroundJobService
{
    string EnqueueConnectorSync(Guid connectorId);
    string EnqueueRepositoryScan(Guid connectorId, string repositoryName);
    string EnqueueDatabaseScan(Guid connectorId, string databaseName);
    string EnqueueGenerateRequirements(Guid ticketId);
}

public interface IBillingService
{
    IReadOnlyList<PlanPricingDto> GetPlans();
    Task<OrganizationBillingDto> GetOrganizationBillingAsync(Guid organizationId, CancellationToken ct = default);
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(Guid organizationId, Guid userId, CreateCheckoutSessionRequest request, CancellationToken ct = default);
    Task<PortalSessionDto> CreatePortalSessionAsync(Guid organizationId, CreatePortalSessionRequest request, CancellationToken ct = default);
    Task HandleStripeWebhookAsync(string json, string signatureHeader, CancellationToken ct = default);
    Task EnsureStripeCouponsAsync(CancellationToken ct = default);
    Task<AiUsageWithOverageDto> GetAiUsageWithOverageAsync(Guid organizationId, CancellationToken ct = default);
}

public interface IOutboundWebhookService
{
    Task<OutboundWebhookSubscriptionDto> CreateAsync(Guid organizationId, CreateOutboundWebhookRequest request, CancellationToken ct = default);
    Task<List<OutboundWebhookSubscriptionDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, Guid subscriptionId, CancellationToken ct = default);
    Task DispatchAsync(string eventType, Guid organizationId, object payload, CancellationToken ct = default);
}

public interface IComplianceService
{
    Task<int> PurgeExpiredAuditLogsAsync(CancellationToken ct = default);
    Task<string> ExportOrganizationDataAsync(Guid organizationId, CancellationToken ct = default);
    Task DeleteOrganizationDataAsync(Guid organizationId, CancellationToken ct = default);
}

public interface IAdminService
{
    Task<List<AdminOrganizationSummaryDto>> ListOrganizationsAsync(CancellationToken ct = default);
    Task<AdminOrganizationDetailDto?> GetOrganizationDetailAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationDto> OverridePlanAsync(Guid organizationId, OverridePlanRequest request, CancellationToken ct = default);
}
