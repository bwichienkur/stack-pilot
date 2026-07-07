using StackPilot.Application.DTOs;

namespace StackPilot.Application.Interfaces;

public interface IPlanLimitService
{
    Task EnsureCanCreateConnectorAsync(Guid organizationId, CancellationToken ct = default);
    Task EnsureCanCreateWorkspaceAsync(Guid organizationId, CancellationToken ct = default);
    Task EnsureCanUseAiAsync(Guid organizationId, int estimatedTokens = 4_000, CancellationToken ct = default);
    Task EnsureCanAddSeatAsync(Guid organizationId, CancellationToken ct = default);
    Task<PlanEnforcementDto> GetEnforcementStatusAsync(Guid organizationId, CancellationToken ct = default);
}
