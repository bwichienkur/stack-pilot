using StackPilot.Domain.Common;
using StackPilot.Domain.Enums;

namespace StackPilot.Domain.Entities;

public class Ticket : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int TicketNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TicketType TicketType { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Submitted;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public Guid RequesterId { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? BusinessJustification { get; set; }
    public string? AiRequirementsJson { get; set; }
    public string? ImplementationPlanJson { get; set; }
    public decimal? RiskScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? ExternalReference { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ApplicationUser Requester { get; set; } = null!;
    public ApplicationUser? Assignee { get; set; }
    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
    public ICollection<Approval> Approvals { get; set; } = [];
    public ICollection<QaEvidence> QaEvidences { get; set; } = [];
    public ICollection<UatDecision> UatDecisions { get; set; } = [];
    public ICollection<BuildRun> BuildRuns { get; set; } = [];
    public ICollection<ReleaseSchedule> ReleaseSchedules { get; set; } = [];
}

public class TicketComment : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public Guid UploadedBy { get; set; }

    public Ticket Ticket { get; set; } = null!;
}

public class Approval : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid TicketId { get; set; }
    public ApprovalType ApprovalType { get; set; }
    public Guid ApproverId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Comments { get; set; }
    public int? PlanVersion { get; set; }
    public decimal? RiskScore { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Approver { get; set; } = null!;
}

public class ApprovalGate : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public ApprovalType GateType { get; set; }
    public string RequiredPermission { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class QaEvidence : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid TesterId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? EvidenceUrlsJson { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Tester { get; set; } = null!;
}

public class UatDecision : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid ApproverId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Comments { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public ApplicationUser Approver { get; set; } = null!;
}

public class ReleaseSchedule : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid TicketId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? ReleaseWindow { get; set; }
    public string? ChecklistJson { get; set; }
    public string? RollbackPlan { get; set; }
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Scheduled;
    public DateTime? DeployedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Ticket Ticket { get; set; } = null!;
}

public class BuildRun : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? TicketId { get; set; }
    public Guid? ConnectorId { get; set; }
    public string? ExternalId { get; set; }
    public BuildStatus Status { get; set; } = BuildStatus.Queued;
    public string? Conclusion { get; set; }
    public string? LogsUrl { get; set; }
    public string? PullRequestUrl { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Ticket? Ticket { get; set; }
}

public class AiAction : BaseEntity, ITenantEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? Model { get; set; }
    public int? TokensUsed { get; set; }
    public AiActionStatus Status { get; set; } = AiActionStatus.Pending;
    public bool IsReversible { get; set; }
    public Guid? ReversalId { get; set; }
}

public class AiConversation : BaseEntity, ITenantEntity, IWorkspaceScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "New Conversation";
    public string MessagesJson { get; set; } = "[]";
}
