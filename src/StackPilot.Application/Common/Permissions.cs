namespace StackPilot.Application.Common;

public static class Permissions
{
    public const string OrgManage = "org:manage";
    public const string UsersManage = "users:manage";
    public const string ConnectorsManage = "connectors:manage";
    public const string ConnectorsRead = "connectors:read";
    public const string GraphRead = "graph:read";
    public const string GraphManage = "graph:manage";
    public const string DocsRead = "docs:read";
    public const string DocsManage = "docs:manage";
    public const string TicketsCreate = "tickets:create";
    public const string TicketsRead = "tickets:read";
    public const string TicketsManage = "tickets:manage";
    public const string TicketsApproveTechnical = "tickets:approve:technical";
    public const string TicketsApproveSecurity = "tickets:approve:security";
    public const string TicketsApproveDatabase = "tickets:approve:database";
    public const string TicketsQa = "tickets:qa";
    public const string TicketsUat = "tickets:uat";
    public const string TicketsApproveRelease = "tickets:approve:release";
    public const string RecommendationsRead = "recommendations:read";
    public const string RecommendationsManage = "recommendations:manage";
    public const string DeploymentsRead = "deployments:read";
    public const string DeploymentsManage = "deployments:manage";
    public const string AuditRead = "audit:read";
    public const string SettingsManage = "settings:manage";
    public const string DashboardRead = "dashboard:read";
    public const string AiUse = "ai:use";

    public static readonly string[] All =
    [
        OrgManage, UsersManage, ConnectorsManage, ConnectorsRead,
        GraphRead, GraphManage, DocsRead, DocsManage,
        TicketsCreate, TicketsRead, TicketsManage,
        TicketsApproveTechnical, TicketsApproveSecurity, TicketsApproveDatabase,
        TicketsQa, TicketsUat, TicketsApproveRelease,
        RecommendationsRead, RecommendationsManage,
        DeploymentsRead, DeploymentsManage,
        AuditRead, SettingsManage, DashboardRead, AiUse
    ];
}

public static class StackPilotClaimTypes
{
    public const string OrgPermission = "stackpilot:perm";
    public const string OrgRole = "stackpilot:role";
    public const string Organization = "stackpilot:org";
}
