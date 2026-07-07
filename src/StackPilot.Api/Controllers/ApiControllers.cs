using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StackPilot.Api.Authorization;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _auth.LoginAsync(request, ct);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(new ApiError { Code = "INVALID_CREDENTIALS", Message = "Invalid email or password" }));
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
    {
        var user = await _auth.GetCurrentUserAsync(UserId, ct);
        return user is null ? NotFound() : Ok(ApiResponse<UserDto>.Ok(user));
    }

    [HttpPost("refresh-session")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshSession(CancellationToken ct) =>
        Ok(ApiResponse<AuthResponse>.Ok(await _auth.RefreshSessionAsync(UserId, ct)));

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(ApiResponse<AuthResponse>.Ok(await _auth.RefreshWithTokenAsync(request.RefreshToken, ct)));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<AuthResponse>.Fail(new ApiError { Code = "INVALID_REFRESH_TOKEN", Message = "Invalid or expired refresh token" }));
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        await _auth.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgs;
    private readonly IAuthService _auth;

    public OrganizationsController(IOrganizationService orgs, IAuthService auth)
    {
        _orgs = orgs;
        _auth = auth;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<OrganizationDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<OrganizationDto>>.Ok(await _orgs.GetUserOrganizationsAsync(UserId, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrganizationCreatedDto>>> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        var org = await _orgs.CreateAsync(request, UserId, ct);
        var session = await _auth.RefreshSessionAsync(UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = org.Id },
            ApiResponse<OrganizationCreatedDto>.Ok(new OrganizationCreatedDto(org, session.AccessToken)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> Get(Guid id, CancellationToken ct)
    {
        var org = await _orgs.GetByIdAsync(id, UserId, ct);
        return org is null ? NotFound() : Ok(ApiResponse<OrganizationDto>.Ok(org));
    }

    [HttpGet("{id:guid}/workspaces")]
    public async Task<ActionResult<ApiResponse<List<WorkspaceDto>>>> Workspaces(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<WorkspaceDto>>.Ok(await _orgs.GetWorkspacesAsync(id, ct)));

    [HttpPost("{id:guid}/workspaces")]
    [RequirePermission(Permissions.OrgManage)]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> CreateWorkspace(Guid id, [FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var ws = await _orgs.CreateWorkspaceAsync(id, request, ct);
        return Ok(ApiResponse<WorkspaceDto>.Ok(ws));
    }

    [HttpGet("{id:guid}/settings")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<OrganizationSettingsDto>>> GetSettings(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationSettingsDto>.Ok(await _orgs.GetSettingsAsync(id, ct)));

    [HttpPut("{id:guid}/settings")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<OrganizationSettingsDto>>> UpdateSettings(Guid id, [FromBody] UpdateOrganizationSettingsRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationSettingsDto>.Ok(await _orgs.UpdateSettingsAsync(id, request, ct)));

    [HttpGet("{id:guid}/members")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<List<OrganizationMemberDto>>>> Members(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<OrganizationMemberDto>>.Ok(await _orgs.GetMembersAsync(id, ct)));

    [HttpGet("roles")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> InvitableRoles(CancellationToken ct) =>
        Ok(ApiResponse<List<RoleDto>>.Ok(await _orgs.GetInvitableRolesAsync(ct)));

    [HttpPost("{id:guid}/invites")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<OrganizationInviteCreatedDto>>> CreateInvite(Guid id, [FromBody] CreateInviteRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationInviteCreatedDto>.Ok(await _orgs.CreateInviteAsync(id, request, UserId, ct)));

    [HttpGet("{id:guid}/invites")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<List<OrganizationInviteDto>>>> Invites(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<OrganizationInviteDto>>.Ok(await _orgs.GetInvitesAsync(id, ct)));

    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> RevokeInvite(Guid id, Guid inviteId, CancellationToken ct)
    {
        await _orgs.RevokeInviteAsync(id, inviteId, ct);
        return NoContent();
    }

    [HttpPost("invites/accept")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> AcceptInvite([FromBody] AcceptInviteRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationDto>.Ok(await _orgs.AcceptInviteAsync(request, UserId, ct)));

    [HttpPost("{id:guid}/export")]
    [RequirePermission(Permissions.OrgManage)]
    public async Task<IActionResult> ExportData(Guid id, [FromServices] IComplianceService compliance, CancellationToken ct)
    {
        var json = await compliance.ExportOrganizationDataAsync(id, ct);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"stackpilot-export-{id}.json");
    }

    [HttpPost("{id:guid}/delete-data")]
    [RequirePermission(Permissions.OrgManage)]
    public async Task<IActionResult> DeleteOrganizationData(Guid id, [FromServices] IComplianceService compliance, CancellationToken ct)
    {
        await compliance.DeleteOrganizationDataAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/members/role")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<ApiResponse<OrganizationMemberDto>>> UpdateMemberRole(Guid id, [FromBody] UpdateMemberRoleRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationMemberDto>.Ok(await _orgs.UpdateMemberRoleAsync(id, request, ct)));

    [HttpGet("{id:guid}/approval-gates")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<List<ApprovalGateDto>>>> ApprovalGates(Guid id, [FromServices] IApprovalGateService gates, CancellationToken ct) =>
        Ok(ApiResponse<List<ApprovalGateDto>>.Ok(await gates.GetGatesAsync(id, ct)));

    [HttpPut("{id:guid}/approval-gates")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<List<ApprovalGateDto>>>> UpdateApprovalGates(
        Guid id, [FromBody] List<UpdateApprovalGateRequest> gates, [FromServices] IApprovalGateService gateService, CancellationToken ct) =>
        Ok(ApiResponse<List<ApprovalGateDto>>.Ok(await gateService.UpdateGatesAsync(id, gates, ct)));

    [HttpGet("{id:guid}/webhooks")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<List<OutboundWebhookSubscriptionDto>>>> ListWebhooks(
        Guid id, [FromServices] IOutboundWebhookService webhooks, CancellationToken ct) =>
        Ok(ApiResponse<List<OutboundWebhookSubscriptionDto>>.Ok(await webhooks.ListAsync(id, ct)));

    [HttpPost("{id:guid}/webhooks")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<OutboundWebhookSubscriptionDto>>> CreateWebhook(
        Guid id, [FromBody] CreateOutboundWebhookRequest request, [FromServices] IOutboundWebhookService webhooks, CancellationToken ct) =>
        Ok(ApiResponse<OutboundWebhookSubscriptionDto>.Ok(await webhooks.CreateAsync(id, request, ct)));

    [HttpDelete("{id:guid}/webhooks/{subscriptionId:guid}")]
    [RequirePermission(Permissions.SettingsManage)]
    public async Task<IActionResult> DeleteWebhook(Guid id, Guid subscriptionId, [FromServices] IOutboundWebhookService webhooks, CancellationToken ct)
    {
        await webhooks.DeleteAsync(id, subscriptionId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1")]
[Authorize]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorService _connectors;

    public ConnectorsController(IConnectorService connectors) => _connectors = connectors;

    [HttpGet("connectors/definitions")]
    public async Task<ActionResult<ApiResponse<List<ConnectorDefinitionDto>>>> Definitions(CancellationToken ct) =>
        Ok(ApiResponse<List<ConnectorDefinitionDto>>.Ok(await _connectors.GetDefinitionsAsync(ct)));

    [HttpGet("workspaces/{workspaceId:guid}/connectors")]
    [RequirePermission(Permissions.ConnectorsRead)]
    public async Task<ActionResult<ApiResponse<List<ConnectorInstanceDto>>>> List(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<ConnectorInstanceDto>>.Ok(await _connectors.GetByWorkspaceAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/connectors")]
    [RequirePermission(Permissions.ConnectorsManage)]
    public async Task<ActionResult<ApiResponse<ConnectorInstanceDto>>> Create(Guid workspaceId, [FromBody] CreateConnectorRequest request, CancellationToken ct)
    {
        var connector = await _connectors.CreateAsync(workspaceId, request, ct);
        return Ok(ApiResponse<ConnectorInstanceDto>.Ok(connector));
    }

    [HttpPost("connectors/{id:guid}/test")]
    [RequirePermission(Permissions.ConnectorsManage)]
    public async Task<ActionResult<ApiResponse<bool>>> Test(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<bool>.Ok(await _connectors.TestConnectionAsync(id, ct)));

    [HttpPost("connectors/{id:guid}/sync")]
    [RequirePermission(Permissions.ConnectorsManage)]
    public async Task<ActionResult<ApiResponse<SyncHistoryDto>>> Sync(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<SyncHistoryDto>.Ok(await _connectors.TriggerSyncAsync(id, ct)));

    [HttpGet("connectors/{id:guid}/sync-history")]
    [RequirePermission(Permissions.ConnectorsRead)]
    public async Task<ActionResult<ApiResponse<List<SyncHistoryDto>>>> SyncHistory(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<SyncHistoryDto>>.Ok(await _connectors.GetSyncHistoryAsync(id, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/connectors/health")]
    [RequirePermission(Permissions.ConnectorsRead)]
    public async Task<ActionResult<ApiResponse<ConnectorHealthSummaryDto>>> HealthSummary(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<ConnectorHealthSummaryDto>.Ok(await _connectors.GetHealthSummaryAsync(workspaceId, ct)));
}

[ApiController]
[Route("api/v1")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;
    private readonly IAiService _ai;

    public TicketsController(ITicketService tickets, IAiService ai)
    {
        _tickets = tickets;
        _ai = ai;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("workspaces/{workspaceId:guid}/tickets")]
    [RequirePermission(Permissions.TicketsRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> List(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _tickets.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<TicketDto>>.Ok(result, new ApiMeta { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount }));
    }

    [HttpPost("workspaces/{workspaceId:guid}/tickets")]
    [RequirePermission(Permissions.TicketsCreate)]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Create(Guid workspaceId, [FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.CreateAsync(workspaceId, request, UserId, ct);
        return Ok(ApiResponse<TicketDto>.Ok(ticket));
    }

    [HttpGet("tickets/{id:guid}")]
    [RequirePermission(Permissions.TicketsRead)]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Get(Guid id, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ApiResponse<TicketDetailDto>.Ok(ticket));
    }

    [HttpPatch("tickets/{id:guid}")]
    [RequirePermission(Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Update(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.UpdateAsync(id, request, ct);
        return ticket is null ? NotFound() : Ok(ApiResponse<TicketDto>.Ok(ticket));
    }

    [HttpPost("tickets/{id:guid}/comments")]
    [RequirePermission(Permissions.TicketsManage)]
    public async Task<ActionResult<ApiResponse<TicketCommentDto>>> Comment(Guid id, [FromBody] AddCommentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TicketCommentDto>.Ok(await _tickets.AddCommentAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/generate-requirements")]
    [RequirePermission(Permissions.AiUse)]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<ApiResponse<AiRequirementsResult>>> GenerateRequirements(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<AiRequirementsResult>.Ok(await _ai.GenerateRequirementsAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/generate-plan")]
    [RequirePermission(Permissions.AiUse)]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<ApiResponse<string>>> GeneratePlan(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<string>.Ok(await _ai.GenerateImplementationPlanAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/approvals")]
    [RequirePermission(Permissions.TicketsApproveTechnical)]
    public async Task<ActionResult<ApiResponse<ApprovalDto>>> Approve(Guid id, [FromBody] SubmitApprovalRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ApprovalDto>.Ok(await _tickets.SubmitApprovalAsync(id, request, UserId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/approvals/pending")]
    [RequirePermission(Permissions.TicketsApproveTechnical)]
    public async Task<ActionResult<ApiResponse<List<TicketDto>>>> PendingApprovals(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<TicketDto>>.Ok(await _tickets.GetPendingApprovalsAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/qa/pending")]
    [RequirePermission(Permissions.TicketsQa)]
    public async Task<ActionResult<ApiResponse<List<TicketDto>>>> PendingQa(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<TicketDto>>.Ok(await _tickets.GetPendingQaAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/uat/pending")]
    [RequirePermission(Permissions.TicketsUat)]
    public async Task<ActionResult<ApiResponse<List<TicketDto>>>> PendingUat(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<TicketDto>>.Ok(await _tickets.GetPendingUatAsync(workspaceId, ct)));

    [HttpGet("tickets/{id:guid}/build-runs")]
    [RequirePermission(Permissions.TicketsRead)]
    public async Task<ActionResult<ApiResponse<List<BuildRunDto>>>> TicketBuildRuns(Guid id, [FromServices] IIntelligenceService intelligence, CancellationToken ct) =>
        Ok(ApiResponse<List<BuildRunDto>>.Ok(await intelligence.GetBuildRunsByTicketAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/qa")]
    [RequirePermission(Permissions.TicketsQa)]
    public async Task<ActionResult<ApiResponse<QaEvidenceDto>>> Qa(Guid id, [FromBody] SubmitQaRequest request, CancellationToken ct) =>
        Ok(ApiResponse<QaEvidenceDto>.Ok(await _tickets.SubmitQaAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/uat")]
    [RequirePermission(Permissions.TicketsUat)]
    public async Task<ActionResult<ApiResponse<UatDecisionDto>>> Uat(Guid id, [FromBody] SubmitUatRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UatDecisionDto>.Ok(await _tickets.SubmitUatAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/schedule-release")]
    [RequirePermission(Permissions.TicketsApproveRelease)]
    public async Task<ActionResult<ApiResponse<ReleaseScheduleDto>>> ScheduleRelease(Guid id, [FromBody] ScheduleReleaseRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ReleaseScheduleDto>.Ok(await _tickets.ScheduleReleaseAsync(id, request, UserId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/releases")]
    [RequirePermission(Permissions.TicketsApproveRelease)]
    public async Task<ActionResult<ApiResponse<List<ReleaseScheduleDetailDto>>>> Releases(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<ReleaseScheduleDetailDto>>.Ok(await _tickets.GetScheduledReleasesAsync(workspaceId, ct)));

    [HttpGet("tickets/{id:guid}/workflow")]
    [RequirePermission(Permissions.TicketsRead)]
    public async Task<ActionResult<ApiResponse<TicketWorkflowDto>>> Workflow(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<TicketWorkflowDto>.Ok(await _tickets.GetWorkflowAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/releases/{releaseId:guid}")]
    [RequirePermission(Permissions.TicketsApproveRelease)]
    public async Task<ActionResult<ApiResponse<ReleaseScheduleDto>>> UpdateRelease(Guid id, Guid releaseId, [FromBody] UpdateReleaseRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ReleaseScheduleDto>.Ok(await _tickets.UpdateReleaseAsync(id, releaseId, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/generate-code")]
    [RequirePermission(Permissions.AiUse)]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<ApiResponse<AiCodeSuggestionDto>>> GenerateCode(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<AiCodeSuggestionDto>.Ok(await _ai.GenerateCodeAsync(id, ct)));
}

[ApiController]
[Route("api/v1")]
[Authorize]
public class GraphController : ControllerBase
{
    private readonly IGraphService _graph;
    private readonly IDashboardService _dashboard;
    private readonly IIntelligenceService _intelligence;
    private readonly IRecommendationService _recommendations;
    private readonly IDocumentationService _docs;
    private readonly IAiService _ai;
    private readonly IAuditService _audit;

    public GraphController(IGraphService graph, IDashboardService dashboard, IIntelligenceService intelligence,
        IRecommendationService recommendations, IDocumentationService docs, IAiService ai, IAuditService audit)
    {
        _graph = graph;
        _dashboard = dashboard;
        _intelligence = intelligence;
        _recommendations = recommendations;
        _docs = docs;
        _ai = ai;
        _audit = audit;
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("workspaces/{workspaceId:guid}/dashboard")]
    [RequirePermission(Permissions.DashboardRead)]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Dashboard(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<DashboardStatsDto>.Ok(await _dashboard.GetStatsAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/graph/nodes")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<GraphNodeDto>>>> Nodes(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _graph.GetNodesAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<GraphNodeDto>>.Ok(result));
    }

    [HttpGet("workspaces/{workspaceId:guid}/graph/edges")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<List<GraphEdgeDto>>>> Edges(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<GraphEdgeDto>>.Ok(await _graph.GetEdgesAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/graph/search")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<List<GraphNodeDto>>>> Search(Guid workspaceId, [FromBody] GraphSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<List<GraphNodeDto>>.Ok(await _graph.SearchAsync(workspaceId, request, ct)));

    [HttpPost("graph/nodes/{nodeId:guid}/impact")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<ImpactAnalysisDto>>> Impact(Guid nodeId, CancellationToken ct) =>
        Ok(ApiResponse<ImpactAnalysisDto>.Ok(await _graph.AnalyzeImpactAsync(nodeId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/repositories")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<List<RepositoryScanDto>>>> Repositories(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<RepositoryScanDto>>.Ok(await _intelligence.GetRepositoryScansAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/scans/repository")]
    [RequirePermission(Permissions.GraphManage)]
    public async Task<ActionResult<ApiResponse<RepositoryScanDto>>> TriggerRepositoryScan(Guid workspaceId, [FromBody] TriggerRepositoryScanRequest request, CancellationToken ct) =>
        Ok(ApiResponse<RepositoryScanDto>.Ok(await _intelligence.TriggerRepositoryScanAsync(request.ConnectorId, request.RepositoryName, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/scans/database")]
    [RequirePermission(Permissions.GraphManage)]
    public async Task<ActionResult<ApiResponse<DatabaseScanDto>>> TriggerDatabaseScan(Guid workspaceId, [FromBody] TriggerDatabaseScanRequest request, CancellationToken ct) =>
        Ok(ApiResponse<DatabaseScanDto>.Ok(await _intelligence.TriggerDatabaseScanAsync(request.ConnectorId, request.DatabaseName, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/databases")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<List<DatabaseScanDto>>>> Databases(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<DatabaseScanDto>>.Ok(await _intelligence.GetDatabaseScansAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/build-runs")]
    [RequirePermission(Permissions.DeploymentsRead)]
    public async Task<ActionResult<ApiResponse<List<BuildRunDto>>>> BuildRuns(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<BuildRunDto>>.Ok(await _intelligence.GetBuildRunsAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/applications")]
    [RequirePermission(Permissions.GraphRead)]
    public async Task<ActionResult<ApiResponse<List<GraphNodeDto>>>> Applications(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<GraphNodeDto>>.Ok(await _graph.GetApplicationsAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/recommendations/generate")]
    [RequirePermission(Permissions.RecommendationsManage)]
    public async Task<ActionResult<ApiResponse<List<RecommendationDto>>>> GenerateRecommendations(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<RecommendationDto>>.Ok(await _recommendations.GenerateAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/recommendations")]
    [RequirePermission(Permissions.RecommendationsRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<RecommendationDto>>>> Recommendations(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _recommendations.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<RecommendationDto>>.Ok(result));
    }

    [HttpGet("workspaces/{workspaceId:guid}/docs")]
    [RequirePermission(Permissions.DocsRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentationPageDto>>>> Docs(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _docs.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<DocumentationPageDto>>.Ok(result));
    }

    [HttpPost("docs/{pageId:guid}/generate")]
    [RequirePermission(Permissions.DocsManage)]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<ApiResponse<string>>> GenerateDoc(Guid pageId, CancellationToken ct) =>
        Ok(ApiResponse<string>.Ok(await _ai.GenerateDocumentationAsync(pageId, ct)));

    [HttpGet("docs/{pageId:guid}/latest")]
    [RequirePermission(Permissions.DocsRead)]
    public async Task<ActionResult<ApiResponse<DocumentationVersionDto>>> LatestDoc(Guid pageId, CancellationToken ct)
    {
        var version = await _docs.GetLatestVersionAsync(pageId, ct);
        return version is null ? NotFound() : Ok(ApiResponse<DocumentationVersionDto>.Ok(version));
    }

    [HttpPost("workspaces/{workspaceId:guid}/ai/chat")]
    [RequirePermission(Permissions.AiUse)]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> Chat(Guid workspaceId, [FromBody] AiChatRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AiChatResponse>.Ok(await _ai.ChatAsync(workspaceId, request, UserId, ct)));

    [HttpGet("organizations/{orgId:guid}/audit-logs")]
    [RequirePermission(Permissions.AuditRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> AuditLogs(Guid orgId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _audit.GetLogsAsync(orgId, request, ct);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}
