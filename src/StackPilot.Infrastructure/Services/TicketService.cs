using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Application.Connectors;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Application.Workflow;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Connectors;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly IBackgroundJobService _jobs;
    private readonly IPermissionValidator _permissions;
    private readonly INotificationService _notifications;
    private readonly IApprovalGateService _approvalGates;
    private readonly IOutboundWebhookService _outboundWebhooks;
    private readonly JiraConnector _jiraConnector;
    private readonly ServiceNowConnector _serviceNowConnector;
    private readonly ICredentialEncryptionService _encryption;

    public TicketService(AppDbContext db, ITenantContext tenant, IAuditService audit, IBackgroundJobService jobs, IPermissionValidator permissions, INotificationService notifications, IApprovalGateService approvalGates, IOutboundWebhookService outboundWebhooks, JiraConnector jiraConnector, ServiceNowConnector serviceNowConnector, ICredentialEncryptionService encryption)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _jobs = jobs;
        _permissions = permissions;
        _notifications = notifications;
        _approvalGates = approvalGates;
        _outboundWebhooks = outboundWebhooks;
        _jiraConnector = jiraConnector;
        _serviceNowConnector = serviceNowConnector;
        _encryption = encryption;
    }

    public async Task<PagedResult<TicketDto>> GetByWorkspaceAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Tickets.Where(t => t.WorkspaceId == workspaceId);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(t => t.Title.Contains(request.Search));

        var total = await query.CountAsync(ct);
        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = tickets.Select(MapTicket).ToList();

        return new PagedResult<TicketDto> { Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize };
    }

    public async Task<TicketDetailDto?> GetByIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Comments)
            .Include(t => t.Approvals)
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

        if (ticket is null) return null;

        return new TicketDetailDto(
            ticket.Id, ticket.TicketNumber, ticket.Title, ticket.Description,
            ticket.TicketType.ToString(), ticket.Status.ToString(), ticket.Priority.ToString(),
            ticket.RequesterId, ticket.AssigneeId, ticket.RiskScore, ticket.ConfidenceScore,
            ticket.CreatedAt, ticket.UpdatedAt,
            ticket.BusinessJustification, ticket.AiRequirementsJson, ticket.ImplementationPlanJson,
            ticket.Comments.Select(c => new TicketCommentDto(c.Id, c.UserId, c.Content, c.CreatedAt)).ToList(),
            ticket.Approvals.Select(a => new ApprovalDto(a.Id, a.ApprovalType.ToString(), a.ApproverId, a.Decision.ToString(), a.Comments, a.DecidedAt)).ToList());
    }

    public async Task<TicketDto> CreateAsync(Guid workspaceId, CreateTicketRequest request, Guid requesterId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.FindAsync([workspaceId], ct)
            ?? throw new KeyNotFoundException("Workspace not found");

        if (_tenant.OrganizationId is Guid tenantOrgId && tenantOrgId != ws.OrganizationId)
            throw new UnauthorizedAccessException("Workspace does not belong to the current organization");

        _tenant.SetOrganization(ws.OrganizationId);

        var ticket = new Ticket
        {
            OrganizationId = ws.OrganizationId,
            WorkspaceId = workspaceId,
            Title = request.Title,
            Description = request.Description,
            TicketType = Enum.Parse<TicketType>(request.TicketType, true),
            Priority = Enum.Parse<TicketPriority>(request.Priority, true),
            RequesterId = requesterId,
            BusinessJustification = request.BusinessJustification,
            Status = TicketStatus.AiAnalysisPending
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ticket.created", "Ticket", ticket.Id, ct: ct);

        _jobs.EnqueueGenerateRequirements(ticket.Id);
        return MapTicket(ticket);
    }

    public async Task<TicketDto?> UpdateAsync(Guid ticketId, UpdateTicketRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct);
        if (ticket is null) return null;

        if (request.Title is not null) ticket.Title = request.Title;
        if (request.Description is not null) ticket.Description = request.Description;
        if (request.Status is not null)
        {
            var newStatus = Enum.Parse<TicketStatus>(request.Status, true);
            TicketStateMachine.ValidateTransition(ticket.Status, newStatus);
            var previousStatus = ticket.Status;
            ticket.Status = newStatus;
            await PushExternalStatusIfLinkedAsync(ticket, ct);
            await _outboundWebhooks.DispatchAsync("ticket.status_changed", ticket.OrganizationId,
                new { ticketId = ticket.Id, from = previousStatus.ToString(), to = newStatus.ToString() }, ct);
        }
        if (request.Priority is not null) ticket.Priority = Enum.Parse<TicketPriority>(request.Priority, true);
        if (request.AssigneeId.HasValue) ticket.AssigneeId = request.AssigneeId;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ticket.updated", "Ticket", ticket.Id, ct: ct);
        return MapTicket(ticket);
    }

    public async Task<TicketCommentDto> AddCommentAsync(Guid ticketId, AddCommentRequest request, Guid userId, CancellationToken ct = default)
    {
        var comment = new TicketComment { TicketId = ticketId, UserId = userId, Content = request.Content };
        _db.TicketComments.Add(comment);
        await _db.SaveChangesAsync(ct);
        return new TicketCommentDto(comment.Id, comment.UserId, comment.Content, comment.CreatedAt);
    }

    public async Task<List<TicketDto>> GetPendingApprovalsAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var tickets = await _db.Tickets
            .Where(t => t.WorkspaceId == workspaceId &&
                (t.Status == TicketStatus.AwaitingApproval || t.Status == TicketStatus.RequirementsDrafted))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(MapTicket).ToList();
    }

    public async Task<List<TicketDto>> GetPendingQaAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var tickets = await _db.Tickets
            .Where(t => t.WorkspaceId == workspaceId &&
                (t.Status == TicketStatus.DeployedToTest || t.Status == TicketStatus.QaInProgress || t.Status == TicketStatus.QaFailed))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(MapTicket).ToList();
    }

    public async Task<List<TicketDto>> GetPendingUatAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var tickets = await _db.Tickets
            .Where(t => t.WorkspaceId == workspaceId &&
                (t.Status == TicketStatus.QaPassed || t.Status == TicketStatus.UatInProgress || t.Status == TicketStatus.UatRejected))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tickets.Select(MapTicket).ToList();
    }

    public async Task<ApprovalDto> SubmitApprovalAsync(Guid ticketId, SubmitApprovalRequest request, Guid approverId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var approvalType = Enum.Parse<ApprovalType>(request.ApprovalType, true);
        var requiredPermission = PermissionForApprovalType(approvalType);
        await _permissions.EnsurePermissionAsync(approverId, ticket.OrganizationId, requiredPermission, ct);

        var decision = Enum.Parse<ApprovalDecision>(request.Decision, true);
        var approval = new Approval
        {
            OrganizationId = ticket.OrganizationId,
            TicketId = ticketId,
            ApprovalType = Enum.Parse<ApprovalType>(request.ApprovalType, true),
            ApproverId = approverId,
            Decision = decision,
            Comments = request.Comments,
            RiskScore = ticket.RiskScore
        };

        _db.Approvals.Add(approval);
        await _db.SaveChangesAsync(ct);

        if (decision == ApprovalDecision.Approved)
        {
            var allSatisfied = await _approvalGates.AreAllGatesSatisfiedAsync(ticketId, ct);
            ticket.Status = allSatisfied ? TicketStatus.Approved : TicketStatus.AwaitingApproval;
        }
        else
            ticket.Status = TicketStatus.RequirementsDrafted;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync($"approval.{decision.ToString().ToLowerInvariant()}", "Ticket", ticketId,
            JsonSerializer.Serialize(new { request.ApprovalType }), ct);

        await _notifications.NotifyAsync(
            "Ticket Approval",
            $"Ticket #{ticket.TicketNumber} \"{ticket.Title}\" was {decision} ({request.ApprovalType})",
            ticket.OrganizationId, ct);

        return new ApprovalDto(approval.Id, approval.ApprovalType.ToString(), approval.ApproverId,
            approval.Decision.ToString(), approval.Comments, approval.DecidedAt);
    }

    public async Task<QaEvidenceDto> SubmitQaAsync(Guid ticketId, SubmitQaRequest request, Guid testerId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var evidence = new QaEvidence
        {
            TicketId = ticketId,
            TesterId = testerId,
            Result = request.Result,
            Notes = request.Notes,
            EvidenceUrlsJson = request.EvidenceUrls is not null ? JsonSerializer.Serialize(request.EvidenceUrls) : null
        };

        _db.QaEvidences.Add(evidence);
        ticket.Status = request.Result.Equals("pass", StringComparison.OrdinalIgnoreCase)
            ? TicketStatus.QaPassed : TicketStatus.QaFailed;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("qa.submitted", "Ticket", ticketId, ct: ct);

        return new QaEvidenceDto(evidence.Id, evidence.TesterId, evidence.Result, evidence.Notes, evidence.CreatedAt);
    }

    public async Task<UatDecisionDto> SubmitUatAsync(Guid ticketId, SubmitUatRequest request, Guid approverId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var decision = Enum.Parse<ApprovalDecision>(request.Decision, true);
        var uat = new UatDecision
        {
            TicketId = ticketId,
            ApproverId = approverId,
            Decision = decision,
            Comments = request.Comments
        };

        _db.UatDecisions.Add(uat);
        ticket.Status = decision == ApprovalDecision.Approved
            ? TicketStatus.UatAccepted : TicketStatus.UatRejected;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync($"uat.{decision.ToString().ToLowerInvariant()}", "Ticket", ticketId, ct: ct);

        return new UatDecisionDto(uat.Id, uat.ApproverId, uat.Decision.ToString(), uat.Comments, uat.DecidedAt);
    }

    public async Task<ReleaseScheduleDto> ScheduleReleaseAsync(Guid ticketId, ScheduleReleaseRequest request, Guid userId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (ticket.Status != TicketStatus.UatAccepted)
            throw new InvalidOperationException("Ticket must have UAT accepted before scheduling a release");

        var release = new ReleaseSchedule
        {
            OrganizationId = ticket.OrganizationId,
            TicketId = ticketId,
            ScheduledAt = request.ScheduledAt,
            ReleaseWindow = request.ReleaseWindow,
            RollbackPlan = request.RollbackPlan,
            ChecklistJson = request.ChecklistJson,
            CreatedByUserId = userId
        };

        _db.ReleaseSchedules.Add(release);
        ticket.Status = TicketStatus.ScheduledForProduction;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("release.scheduled", "Ticket", ticketId, ct: ct);

        return new ReleaseScheduleDto(release.Id, release.TicketId, release.ScheduledAt, release.ReleaseWindow, release.Status.ToString());
    }

    public async Task<List<ReleaseScheduleDetailDto>> GetScheduledReleasesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await _db.ReleaseSchedules
            .Where(r => r.Ticket.WorkspaceId == workspaceId)
            .OrderBy(r => r.ScheduledAt)
            .Select(r => new ReleaseScheduleDetailDto(
                r.Id, r.TicketId, r.Ticket.TicketNumber, r.Ticket.Title, r.Ticket.Status.ToString(),
                r.ScheduledAt, r.ReleaseWindow, r.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task<TicketWorkflowDto> GetWorkflowAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        return new TicketWorkflowDto(ticket.Status.ToString(), TicketStateMachine.GetAllowedNextStatuses(ticket.Status));
    }

    public async Task<ReleaseScheduleDto> UpdateReleaseAsync(Guid ticketId, Guid releaseId, UpdateReleaseRequest request, Guid userId, CancellationToken ct = default)
    {
        var release = await _db.ReleaseSchedules
            .Include(r => r.Ticket)
            .FirstOrDefaultAsync(r => r.Id == releaseId && r.TicketId == ticketId, ct)
            ?? throw new KeyNotFoundException("Release schedule not found");

        var ticket = release.Ticket;
        var action = request.Action.ToLowerInvariant();

        switch (action)
        {
            case "deploy":
                release.Status = ReleaseStatus.Deployed;
                release.DeployedAt = DateTime.UtcNow;
                TicketStateMachine.ValidateTransition(ticket.Status, TicketStatus.DeployedToProduction);
                ticket.Status = TicketStatus.DeployedToProduction;
                break;
            case "verify":
                release.Status = ReleaseStatus.Verified;
                release.VerifiedAt = DateTime.UtcNow;
                TicketStateMachine.ValidateTransition(ticket.Status, TicketStatus.Closed);
                ticket.Status = TicketStatus.Closed;
                break;
            case "rollback":
                release.Status = ReleaseStatus.RolledBack;
                ticket.Status = TicketStatus.ScheduledForProduction;
                break;
            default:
                throw new ArgumentException($"Unknown release action: {request.Action}");
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync($"release.{action}", "ReleaseSchedule", release.Id, ct: ct);
        await _outboundWebhooks.DispatchAsync($"release.{action}", ticket.OrganizationId,
            new { ticketId, releaseId = release.Id, action }, ct);

        return new ReleaseScheduleDto(release.Id, release.TicketId, release.ScheduledAt, release.ReleaseWindow, release.Status.ToString());
    }

    private async Task PushExternalStatusIfLinkedAsync(Ticket ticket, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ticket.ExternalReference)) return;

        var jiraConnector = await _db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.WorkspaceId == ticket.WorkspaceId && c.Definition.Type == "jira", ct);

        if (jiraConnector is not null)
        {
            var credentials = jiraConnector.Credentials.ToDictionary(
                c => c.CredentialType,
                c => _encryption.Decrypt(c.EncryptedValue, jiraConnector.OrganizationId));

            await _jiraConnector.PushTicketStatusAsync(new ConnectorContext
            {
                OrganizationId = jiraConnector.OrganizationId,
                WorkspaceId = jiraConnector.WorkspaceId,
                ConnectorInstanceId = jiraConnector.Id,
                ConnectorType = "jira",
                ConfigJson = jiraConnector.ConfigJson,
                Credentials = credentials
            }, ticket.ExternalReference, ticket.Status.ToString(), ct);
            return;
        }

        var snowConnector = await _db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.WorkspaceId == ticket.WorkspaceId && c.Definition.Type == "servicenow", ct);

        if (snowConnector is null) return;

        var snowCredentials = snowConnector.Credentials.ToDictionary(
            c => c.CredentialType,
            c => _encryption.Decrypt(c.EncryptedValue, snowConnector.OrganizationId));

        await _serviceNowConnector.PushTicketStatusAsync(new ConnectorContext
        {
            OrganizationId = snowConnector.OrganizationId,
            WorkspaceId = snowConnector.WorkspaceId,
            ConnectorInstanceId = snowConnector.Id,
            ConnectorType = "servicenow",
            ConfigJson = snowConnector.ConfigJson,
            Credentials = snowCredentials
        }, ticket.ExternalReference, ticket.Status.ToString(), ct);
    }

    private static TicketDto MapTicket(Ticket t) => new(
        t.Id, t.TicketNumber, t.Title, t.Description, t.TicketType.ToString(), t.Status.ToString(),
        t.Priority.ToString(), t.RequesterId, t.AssigneeId, t.RiskScore, t.ConfidenceScore, t.CreatedAt, t.UpdatedAt);

    private static string PermissionForApprovalType(ApprovalType type) => type switch
    {
        ApprovalType.Security => Permissions.TicketsApproveSecurity,
        ApprovalType.Database => Permissions.TicketsApproveDatabase,
        ApprovalType.ProductionRelease => Permissions.TicketsApproveRelease,
        _ => Permissions.TicketsApproveTechnical
    };
}
