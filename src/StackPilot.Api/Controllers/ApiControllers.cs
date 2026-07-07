using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _auth.RegisterAsync(request, ct);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AuthResponse>.Fail(new ApiError { Code = "VALIDATION_ERROR", Message = ex.Message }));
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
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
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _auth.GetCurrentUserAsync(userId, ct);
        return user is null ? NotFound() : Ok(ApiResponse<UserDto>.Ok(user));
    }
}

[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgs;

    public OrganizationsController(IOrganizationService orgs) => _orgs = orgs;

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<OrganizationDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<OrganizationDto>>.Ok(await _orgs.GetUserOrganizationsAsync(UserId, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        var org = await _orgs.CreateAsync(request, UserId, ct);
        return CreatedAtAction(nameof(Get), new { id = org.Id }, ApiResponse<OrganizationDto>.Ok(org));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> Get(Guid id, CancellationToken ct)
    {
        var org = await _orgs.GetByIdAsync(id, ct);
        return org is null ? NotFound() : Ok(ApiResponse<OrganizationDto>.Ok(org));
    }

    [HttpGet("{id:guid}/workspaces")]
    public async Task<ActionResult<ApiResponse<List<WorkspaceDto>>>> Workspaces(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<WorkspaceDto>>.Ok(await _orgs.GetWorkspacesAsync(id, ct)));

    [HttpPost("{id:guid}/workspaces")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> CreateWorkspace(Guid id, [FromBody] CreateWorkspaceRequest request, CancellationToken ct)
    {
        var ws = await _orgs.CreateWorkspaceAsync(id, request, ct);
        return Ok(ApiResponse<WorkspaceDto>.Ok(ws));
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
    public async Task<ActionResult<ApiResponse<List<ConnectorInstanceDto>>>> List(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<ConnectorInstanceDto>>.Ok(await _connectors.GetByWorkspaceAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/connectors")]
    public async Task<ActionResult<ApiResponse<ConnectorInstanceDto>>> Create(Guid workspaceId, [FromBody] CreateConnectorRequest request, CancellationToken ct)
    {
        var connector = await _connectors.CreateAsync(workspaceId, request, ct);
        return Ok(ApiResponse<ConnectorInstanceDto>.Ok(connector));
    }

    [HttpPost("connectors/{id:guid}/test")]
    public async Task<ActionResult<ApiResponse<bool>>> Test(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<bool>.Ok(await _connectors.TestConnectionAsync(id, ct)));

    [HttpPost("connectors/{id:guid}/sync")]
    public async Task<ActionResult<ApiResponse<SyncHistoryDto>>> Sync(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<SyncHistoryDto>.Ok(await _connectors.TriggerSyncAsync(id, ct)));

    [HttpGet("connectors/{id:guid}/sync-history")]
    public async Task<ActionResult<ApiResponse<List<SyncHistoryDto>>>> SyncHistory(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<SyncHistoryDto>>.Ok(await _connectors.GetSyncHistoryAsync(id, ct)));
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
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> List(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _tickets.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<TicketDto>>.Ok(result, new ApiMeta { Page = result.Page, PageSize = result.PageSize, TotalCount = result.TotalCount }));
    }

    [HttpPost("workspaces/{workspaceId:guid}/tickets")]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Create(Guid workspaceId, [FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.CreateAsync(workspaceId, request, UserId, ct);
        return Ok(ApiResponse<TicketDto>.Ok(ticket));
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> Get(Guid id, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ApiResponse<TicketDetailDto>.Ok(ticket));
    }

    [HttpPatch("tickets/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TicketDto>>> Update(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var ticket = await _tickets.UpdateAsync(id, request, ct);
        return ticket is null ? NotFound() : Ok(ApiResponse<TicketDto>.Ok(ticket));
    }

    [HttpPost("tickets/{id:guid}/comments")]
    public async Task<ActionResult<ApiResponse<TicketCommentDto>>> Comment(Guid id, [FromBody] AddCommentRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TicketCommentDto>.Ok(await _tickets.AddCommentAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/generate-requirements")]
    public async Task<ActionResult<ApiResponse<AiRequirementsResult>>> GenerateRequirements(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<AiRequirementsResult>.Ok(await _ai.GenerateRequirementsAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/generate-plan")]
    public async Task<ActionResult<ApiResponse<string>>> GeneratePlan(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<string>.Ok(await _ai.GenerateImplementationPlanAsync(id, ct)));

    [HttpPost("tickets/{id:guid}/approvals")]
    public async Task<ActionResult<ApiResponse<ApprovalDto>>> Approve(Guid id, [FromBody] SubmitApprovalRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ApprovalDto>.Ok(await _tickets.SubmitApprovalAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/qa")]
    public async Task<ActionResult<ApiResponse<QaEvidenceDto>>> Qa(Guid id, [FromBody] SubmitQaRequest request, CancellationToken ct) =>
        Ok(ApiResponse<QaEvidenceDto>.Ok(await _tickets.SubmitQaAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/uat")]
    public async Task<ActionResult<ApiResponse<UatDecisionDto>>> Uat(Guid id, [FromBody] SubmitUatRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UatDecisionDto>.Ok(await _tickets.SubmitUatAsync(id, request, UserId, ct)));

    [HttpPost("tickets/{id:guid}/schedule-release")]
    public async Task<ActionResult<ApiResponse<ReleaseScheduleDto>>> ScheduleRelease(Guid id, [FromBody] ScheduleReleaseRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ReleaseScheduleDto>.Ok(await _tickets.ScheduleReleaseAsync(id, request, UserId, ct)));
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
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> Dashboard(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<DashboardStatsDto>.Ok(await _dashboard.GetStatsAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/graph/nodes")]
    public async Task<ActionResult<ApiResponse<PagedResult<GraphNodeDto>>>> Nodes(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _graph.GetNodesAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<GraphNodeDto>>.Ok(result));
    }

    [HttpGet("workspaces/{workspaceId:guid}/graph/edges")]
    public async Task<ActionResult<ApiResponse<List<GraphEdgeDto>>>> Edges(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<GraphEdgeDto>>.Ok(await _graph.GetEdgesAsync(workspaceId, ct)));

    [HttpPost("workspaces/{workspaceId:guid}/graph/search")]
    public async Task<ActionResult<ApiResponse<List<GraphNodeDto>>>> Search(Guid workspaceId, [FromBody] GraphSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<List<GraphNodeDto>>.Ok(await _graph.SearchAsync(workspaceId, request, ct)));

    [HttpPost("graph/nodes/{nodeId:guid}/impact")]
    public async Task<ActionResult<ApiResponse<ImpactAnalysisDto>>> Impact(Guid nodeId, CancellationToken ct) =>
        Ok(ApiResponse<ImpactAnalysisDto>.Ok(await _graph.AnalyzeImpactAsync(nodeId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/repositories")]
    public async Task<ActionResult<ApiResponse<List<RepositoryScanDto>>>> Repositories(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<RepositoryScanDto>>.Ok(await _intelligence.GetRepositoryScansAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/databases")]
    public async Task<ActionResult<ApiResponse<List<DatabaseScanDto>>>> Databases(Guid workspaceId, CancellationToken ct) =>
        Ok(ApiResponse<List<DatabaseScanDto>>.Ok(await _intelligence.GetDatabaseScansAsync(workspaceId, ct)));

    [HttpGet("workspaces/{workspaceId:guid}/recommendations")]
    public async Task<ActionResult<ApiResponse<PagedResult<RecommendationDto>>>> Recommendations(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _recommendations.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<RecommendationDto>>.Ok(result));
    }

    [HttpGet("workspaces/{workspaceId:guid}/docs")]
    public async Task<ActionResult<ApiResponse<PagedResult<DocumentationPageDto>>>> Docs(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _docs.GetByWorkspaceAsync(workspaceId, request, ct);
        return Ok(ApiResponse<PagedResult<DocumentationPageDto>>.Ok(result));
    }

    [HttpPost("workspaces/{workspaceId:guid}/ai/chat")]
    public async Task<ActionResult<ApiResponse<AiChatResponse>>> Chat(Guid workspaceId, [FromBody] AiChatRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AiChatResponse>.Ok(await _ai.ChatAsync(workspaceId, request, UserId, ct)));

    [HttpGet("organizations/{orgId:guid}/audit-logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> AuditLogs(Guid orgId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _audit.GetLogsAsync(orgId, request, ct);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}
