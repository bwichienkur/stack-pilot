using StackPilot.Application.DTOs;

namespace StackPilot.Application.Interfaces;

public interface IPermissionValidator
{
    Task<bool> UserHasPermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default);
    Task EnsurePermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default);
}

public interface IApprovalGateService
{
    Task EnsureDefaultGatesAsync(Guid organizationId, CancellationToken ct = default);
    Task<List<ApprovalGateDto>> GetGatesAsync(Guid organizationId, CancellationToken ct = default);
    Task<List<ApprovalGateDto>> UpdateGatesAsync(Guid organizationId, List<UpdateApprovalGateRequest> gates, CancellationToken ct = default);
    Task<bool> AreAllGatesSatisfiedAsync(Guid ticketId, CancellationToken ct = default);
    Task<List<string>> GetPendingGateTypesAsync(Guid ticketId, CancellationToken ct = default);
}

public interface IRagIndexService
{
    Task IndexRepositoryScanAsync(Guid organizationId, Guid workspaceId, Guid? graphNodeId, string repositoryName, string scanResultsJson, CancellationToken ct = default);
    Task<List<RagSearchResult>> SearchAsync(Guid workspaceId, string query, int topK = 10, CancellationToken ct = default);
}

public record RagSearchResult(Guid? GraphNodeId, string Content, double Score);
