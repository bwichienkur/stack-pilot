using Microsoft.EntityFrameworkCore;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db) => _db = db;

    public async Task<List<AdminOrganizationSummaryDto>> ListOrganizationsAsync(CancellationToken ct = default)
    {
        return await _db.Organizations
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new AdminOrganizationSummaryDto(
                o.Id, o.Name, o.Slug, o.Plan.ToString(), o.IsActive,
                o.Members.Count, o.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AdminOrganizationDetailDto?> GetOrganizationDetailAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct);
        if (org is null) return null;

        var memberCount = await _db.OrganizationMembers.CountAsync(m => m.OrganizationId == organizationId, ct);
        var workspaceCount = await _db.Workspaces.IgnoreQueryFilters().CountAsync(w => w.OrganizationId == organizationId, ct);
        var connectorCount = await _db.ConnectorInstances.IgnoreQueryFilters().CountAsync(c => c.OrganizationId == organizationId, ct);

        return new AdminOrganizationDetailDto(
            org.Id, org.Name, org.Slug, org.Plan.ToString(), org.SubscriptionStatus.ToString(),
            org.IsActive, org.TrialEndsAt, memberCount, workspaceCount, connectorCount, org.CreatedAt);
    }

    public async Task<OrganizationDto> OverridePlanAsync(Guid organizationId, OverridePlanRequest request, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FindAsync([organizationId], ct)
            ?? throw new KeyNotFoundException("Organization not found");

        if (!Enum.TryParse<OrganizationPlan>(request.Plan, true, out var plan))
            throw new ArgumentException("Invalid plan");

        org.Plan = plan;
        if (plan != OrganizationPlan.Trial)
        {
            org.SubscriptionStatus = SubscriptionStatus.Active;
            org.TrialEndsAt = null;
        }

        await _db.SaveChangesAsync(ct);
        return new OrganizationDto(org.Id, org.Name, org.Slug, org.Plan.ToString(), org.IsActive);
    }
}
