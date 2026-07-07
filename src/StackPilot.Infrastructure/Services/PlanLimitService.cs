using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Billing;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class PlanLimitService : IPlanLimitService
{
    private readonly AppDbContext _db;

    public PlanLimitService(AppDbContext db) => _db = db;

    public async Task EnsureCanCreateConnectorAsync(Guid organizationId, CancellationToken ct = default)
    {
        var status = await GetEnforcementStatusAsync(organizationId, ct);
        if (status.IsWriteBlocked)
            throw new PlanLimitException("SUBSCRIPTION_BLOCKED", status.BlockReason ?? "Subscription inactive");

        if (status.ConnectorsAtLimit)
        {
            var org = await _db.Organizations.FindAsync([organizationId], ct);
            var max = PlanCatalog.LimitsFor(org!.Plan).MaxConnectors;
            throw new PlanLimitException("CONNECTOR_LIMIT", $"Connector limit reached ({max}). Upgrade your plan to add more.");
        }
    }

    public async Task EnsureCanCreateWorkspaceAsync(Guid organizationId, CancellationToken ct = default)
    {
        var status = await GetEnforcementStatusAsync(organizationId, ct);
        if (status.IsWriteBlocked)
            throw new PlanLimitException("SUBSCRIPTION_BLOCKED", status.BlockReason ?? "Subscription inactive");

        if (status.WorkspacesAtLimit)
        {
            var org = await _db.Organizations.FindAsync([organizationId], ct);
            var max = PlanCatalog.LimitsFor(org!.Plan).MaxWorkspaces;
            throw new PlanLimitException("WORKSPACE_LIMIT", $"Workspace limit reached ({max}). Upgrade your plan to add more.");
        }
    }

    public async Task EnsureCanUseAiAsync(Guid organizationId, int estimatedTokens = 4_000, CancellationToken ct = default)
    {
        var status = await GetEnforcementStatusAsync(organizationId, ct);
        if (status.IsWriteBlocked)
            throw new PlanLimitException("SUBSCRIPTION_BLOCKED", status.BlockReason ?? "Subscription inactive");

        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var limits = PlanCatalog.LimitsFor(org.Plan);
        var usage = await GetUsageAsync(organizationId, ct);

        if (usage.AiTokensUsedThisMonth + estimatedTokens > limits.MonthlyAiTokenBudget)
            throw new PlanLimitException("AI_TOKEN_LIMIT",
                $"Monthly AI token budget exceeded ({limits.MonthlyAiTokenBudget:N0} tokens). Upgrade your plan or wait until next month.");
    }

    public async Task EnsureCanAddSeatAsync(Guid organizationId, CancellationToken ct = default)
    {
        var status = await GetEnforcementStatusAsync(organizationId, ct);
        if (status.IsWriteBlocked)
            throw new PlanLimitException("SUBSCRIPTION_BLOCKED", status.BlockReason ?? "Subscription inactive");

        if (status.SeatsAtLimit)
        {
            var org = await _db.Organizations.FindAsync([organizationId], ct);
            var max = PlanCatalog.LimitsFor(org!.Plan).MaxSeats;
            throw new PlanLimitException("SEAT_LIMIT", $"Seat limit reached ({max}). Upgrade your plan to invite more members.");
        }
    }

    public async Task<PlanEnforcementDto> GetEnforcementStatusAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var limits = PlanCatalog.LimitsFor(org.Plan);
        var usage = await GetUsageAsync(organizationId, ct);
        var (blocked, reason) = GetSubscriptionBlock(org);

        return new PlanEnforcementDto(
            blocked,
            reason,
            usage.SeatCount >= limits.MaxSeats,
            usage.WorkspaceCount >= limits.MaxWorkspaces,
            usage.ConnectorCount >= limits.MaxConnectors,
            usage.AiTokensUsedThisMonth >= limits.MonthlyAiTokenBudget);
    }

    internal static (bool Blocked, string? Reason) GetSubscriptionBlock(Organization org)
    {
        if (org.Plan == OrganizationPlan.Trial)
        {
            if (org.TrialEndsAt is not null && org.TrialEndsAt < DateTime.UtcNow)
                return (true, "Your 14-day trial has ended. Upgrade to continue using StackPilot.");
            return (false, null);
        }

        if (org.Plan == OrganizationPlan.Enterprise)
            return (false, null);

        return org.SubscriptionStatus switch
        {
            SubscriptionStatus.Canceled => (true, "Subscription canceled. Renew to restore write access."),
            SubscriptionStatus.PastDue => (true, "Payment past due. Update billing in the customer portal."),
            SubscriptionStatus.Paused => (true, "Subscription paused. Resume billing to continue."),
            _ => (false, null)
        };
    }

    private async Task<BillingUsageDto> GetUsageAsync(Guid organizationId, CancellationToken ct)
    {
        var seatCount = await _db.OrganizationMembers.CountAsync(m => m.OrganizationId == organizationId, ct);
        var workspaceCount = await _db.Workspaces.CountAsync(w => w.OrganizationId == organizationId, ct);
        var connectorCount = await _db.ConnectorInstances.CountAsync(c => c.OrganizationId == organizationId, ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var aiTokens = await _db.AiActions
            .Where(a => a.OrganizationId == organizationId && a.CreatedAt >= monthStart && a.TokensUsed != null)
            .SumAsync(a => a.TokensUsed ?? 0, ct);

        return new BillingUsageDto(seatCount, workspaceCount, connectorCount, aiTokens);
    }
}
