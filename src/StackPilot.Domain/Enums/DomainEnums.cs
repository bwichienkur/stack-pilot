namespace StackPilot.Domain.Enums;

public enum OrganizationPlan
{
    Trial,
    Starter,
    Professional,
    Enterprise
}

public enum TicketType
{
    Bug,
    Enhancement,
    NewFeature,
    Refactor,
    DatabaseChange,
    SecurityFix,
    PerformanceImprovement,
    DocumentationRequest,
    IntegrationRequest
}

public enum TicketStatus
{
    Submitted,
    AiAnalysisPending,
    RequirementsDrafted,
    AwaitingApproval,
    Approved,
    ImplementationInProgress,
    PullRequestCreated,
    BuildRunning,
    DeployedToTest,
    QaInProgress,
    QaFailed,
    QaPassed,
    UatInProgress,
    UatRejected,
    UatAccepted,
    ScheduledForProduction,
    DeployedToProduction,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConnectorStatus
{
    Pending,
    Active,
    Unhealthy,
    Disabled,
    Error
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

public enum SyncStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Partial
}

public enum ScanStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum GraphNodeType
{
    Organization,
    Application,
    Repository,
    Project,
    Class,
    Method,
    ApiEndpoint,
    Database,
    Table,
    Column,
    StoredProcedure,
    Queue,
    ExternalSystem,
    CloudResource,
    Pipeline,
    Deployment,
    Environment,
    Ticket,
    Requirement,
    Test,
    User,
    Team,
    BusinessCapability,
    DocumentationPage,
    AiRecommendation
}

public enum GraphEdgeType
{
    Calls,
    DependsOn,
    Owns,
    WritesTo,
    ReadsFrom,
    DeployedBy,
    TestedBy,
    RequestedBy,
    ApprovedBy,
    Impacts,
    BelongsTo,
    Implements,
    Documents,
    Monitors
}

public enum ApprovalType
{
    ClientAdmin,
    TechnicalReviewer,
    Security,
    Database,
    Qa,
    Uat,
    ProductionRelease
}

public enum ApprovalDecision
{
    Approved,
    Rejected
}

public enum RecommendationType
{
    Refactor,
    MissingTests,
    SecurityRisk,
    OutdatedDependency,
    DatabaseIndex,
    Performance,
    Architecture,
    CloudCost,
    DeadCode,
    DuplicatedLogic,
    DocumentationGap,
    CiCdImprovement
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RecommendationStatus
{
    Open,
    Accepted,
    Dismissed,
    Implemented
}

public enum AiActionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Reversed
}

public enum DocumentationStatus
{
    Draft,
    Review,
    Published,
    Archived
}

public enum BuildStatus
{
    Queued,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum ReleaseStatus
{
    Scheduled,
    InProgress,
    Deployed,
    Verified,
    RolledBack,
    Cancelled
}

public enum SystemRole
{
    PlatformSuperAdmin,
    ClientAdmin,
    Architect,
    Developer,
    Qa,
    UatApprover,
    BusinessRequester,
    ReadOnlyExecutive
}
