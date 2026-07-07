namespace StackPilot.Application.Interfaces;

public interface IPermissionValidator
{
    Task<bool> UserHasPermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default);
    Task EnsurePermissionAsync(Guid userId, Guid organizationId, string permission, CancellationToken ct = default);
}

public interface IRagIndexService
{
    Task IndexRepositoryScanAsync(Guid organizationId, Guid workspaceId, Guid? graphNodeId, string repositoryName, string scanResultsJson, CancellationToken ct = default);
    Task<List<RagSearchResult>> SearchAsync(Guid workspaceId, string query, int topK = 10, CancellationToken ct = default);
}

public record RagSearchResult(Guid? GraphNodeId, string Content, double Score);
