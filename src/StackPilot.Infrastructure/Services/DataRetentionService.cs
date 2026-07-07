using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackPilot.Application.Billing;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class DataRetentionService : IComplianceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(AppDbContext db, ILogger<DataRetentionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> PurgeExpiredAuditLogsAsync(CancellationToken ct = default)
    {
        var orgs = await _db.Organizations.AsNoTracking().ToListAsync(ct);
        var totalPurged = 0;

        foreach (var org in orgs)
        {
            var retentionDays = PlanCatalog.LimitsFor(org.Plan).AuditRetentionDays;
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            var expired = await _db.AuditLogs
                .IgnoreQueryFilters()
                .Where(l => l.OrganizationId == org.Id && l.CreatedAt < cutoff)
                .ToListAsync(ct);

            if (expired.Count == 0) continue;

            _db.AuditLogs.RemoveRange(expired);
            totalPurged += expired.Count;
        }

        if (totalPurged > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Purged {Count} expired audit log entries", totalPurged);
        }

        return totalPurged;
    }

    public async Task<string> ExportOrganizationDataAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found");

        var export = new
        {
            organization = new { org.Id, org.Name, org.Slug, org.Plan, org.IsActive, org.CreatedAt },
            workspaces = await _db.Workspaces.IgnoreQueryFilters().Where(w => w.OrganizationId == organizationId).ToListAsync(ct),
            members = await _db.OrganizationMembers.IgnoreQueryFilters()
                .Where(m => m.OrganizationId == organizationId)
                .Select(m => new { m.UserId, m.RoleId, m.JoinedAt })
                .ToListAsync(ct),
            tickets = await _db.Tickets.IgnoreQueryFilters().Where(t => t.OrganizationId == organizationId).ToListAsync(ct),
            connectors = await _db.ConnectorInstances.IgnoreQueryFilters()
                .Where(c => c.OrganizationId == organizationId)
                .Select(c => new { c.Id, c.Name, c.DefinitionId, c.Status, c.CreatedAt })
                .ToListAsync(ct),
            auditLogs = await _db.AuditLogs.IgnoreQueryFilters()
                .Where(l => l.OrganizationId == organizationId)
                .OrderByDescending(l => l.CreatedAt)
                .Take(10_000)
                .ToListAsync(ct),
            exportedAt = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task DeleteOrganizationDataAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found");

        org.IsActive = false;
        org.Name = $"{org.Name} (deleted)";
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Soft-deleted organization {OrgId}", organizationId);
    }
}
