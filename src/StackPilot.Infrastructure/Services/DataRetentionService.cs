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

        var workspaceIds = await _db.Workspaces.IgnoreQueryFilters()
            .Where(w => w.OrganizationId == organizationId)
            .Select(w => w.Id)
            .ToListAsync(ct);

        var ticketIds = await _db.Tickets.IgnoreQueryFilters()
            .Where(t => t.OrganizationId == organizationId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var connectorIds = await _db.ConnectorInstances.IgnoreQueryFilters()
            .Where(c => c.OrganizationId == organizationId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        _db.AuditLogs.RemoveRange(await _db.AuditLogs.IgnoreQueryFilters()
            .Where(l => l.OrganizationId == organizationId).ToListAsync(ct));
        _db.AiActions.RemoveRange(await _db.AiActions.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId).ToListAsync(ct));
        _db.AiConversations.RemoveRange(await _db.AiConversations.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == organizationId).ToListAsync(ct));
        _db.GraphChunks.RemoveRange(await _db.GraphChunks.IgnoreQueryFilters()
            .Where(g => g.OrganizationId == organizationId).ToListAsync(ct));
        _db.BuildRuns.RemoveRange(await _db.BuildRuns.IgnoreQueryFilters()
            .Where(b => b.OrganizationId == organizationId).ToListAsync(ct));
        _db.ReleaseSchedules.RemoveRange(await _db.ReleaseSchedules.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId).ToListAsync(ct));
        _db.ApprovalGates.RemoveRange(await _db.ApprovalGates.IgnoreQueryFilters()
            .Where(g => g.OrganizationId == organizationId).ToListAsync(ct));
        _db.OutboundWebhookSubscriptions.RemoveRange(await _db.OutboundWebhookSubscriptions.IgnoreQueryFilters()
            .Where(w => w.OrganizationId == organizationId).ToListAsync(ct));
        _db.OrganizationInvites.RemoveRange(await _db.OrganizationInvites.IgnoreQueryFilters()
            .Where(i => i.OrganizationId == organizationId).ToListAsync(ct));

        if (ticketIds.Count > 0)
        {
            _db.Approvals.RemoveRange(await _db.Approvals.IgnoreQueryFilters()
                .Where(a => ticketIds.Contains(a.TicketId)).ToListAsync(ct));
            _db.QaEvidences.RemoveRange(await _db.QaEvidences.IgnoreQueryFilters()
                .Where(q => ticketIds.Contains(q.TicketId)).ToListAsync(ct));
            _db.UatDecisions.RemoveRange(await _db.UatDecisions.IgnoreQueryFilters()
                .Where(u => ticketIds.Contains(u.TicketId)).ToListAsync(ct));
            _db.TicketComments.RemoveRange(await _db.TicketComments.IgnoreQueryFilters()
                .Where(c => ticketIds.Contains(c.TicketId)).ToListAsync(ct));
            _db.Tickets.RemoveRange(await _db.Tickets.IgnoreQueryFilters()
                .Where(t => ticketIds.Contains(t.Id)).ToListAsync(ct));
        }

        if (connectorIds.Count > 0)
        {
            _db.ConnectorCredentials.RemoveRange(await _db.ConnectorCredentials.IgnoreQueryFilters()
                .Where(c => connectorIds.Contains(c.ConnectorId)).ToListAsync(ct));
            _db.SyncHistories.RemoveRange(await _db.SyncHistories.IgnoreQueryFilters()
                .Where(s => connectorIds.Contains(s.ConnectorId)).ToListAsync(ct));
            _db.ConnectorInstances.RemoveRange(await _db.ConnectorInstances.IgnoreQueryFilters()
                .Where(c => connectorIds.Contains(c.Id)).ToListAsync(ct));
        }

        _db.GraphEdges.RemoveRange(await _db.GraphEdges.IgnoreQueryFilters()
            .Where(e => e.OrganizationId == organizationId).ToListAsync(ct));
        _db.GraphNodes.RemoveRange(await _db.GraphNodes.IgnoreQueryFilters()
            .Where(n => n.OrganizationId == organizationId).ToListAsync(ct));
        var pageIds = await _db.DocumentationPages.IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (pageIds.Count > 0)
        {
            _db.DocumentationVersions.RemoveRange(await _db.DocumentationVersions.IgnoreQueryFilters()
                .Where(v => pageIds.Contains(v.PageId)).ToListAsync(ct));
        }
        _db.DocumentationPages.RemoveRange(await _db.DocumentationPages.IgnoreQueryFilters()
            .Where(p => p.OrganizationId == organizationId).ToListAsync(ct));
        _db.Recommendations.RemoveRange(await _db.Recommendations.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId).ToListAsync(ct));
        _db.RepositoryScans.RemoveRange(await _db.RepositoryScans.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == organizationId).ToListAsync(ct));
        _db.DatabaseScans.RemoveRange(await _db.DatabaseScans.IgnoreQueryFilters()
            .Where(d => d.OrganizationId == organizationId).ToListAsync(ct));
        _db.Workspaces.RemoveRange(await _db.Workspaces.IgnoreQueryFilters()
            .Where(w => workspaceIds.Contains(w.Id)).ToListAsync(ct));

        org.IsActive = false;
        org.Name = $"{org.Name} (deleted)";
        org.SettingsJson = JsonSerializer.Serialize(new { deletedAt = DateTime.UtcNow });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Erased organization data for {OrgId}", organizationId);
    }
}
