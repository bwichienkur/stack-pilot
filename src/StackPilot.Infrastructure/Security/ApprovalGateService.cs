using Microsoft.EntityFrameworkCore;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Security;

public class ApprovalGateService : IApprovalGateService
{
    private readonly AppDbContext _db;

    public ApprovalGateService(AppDbContext db) => _db = db;

    public async Task EnsureDefaultGatesAsync(Guid organizationId, CancellationToken ct = default)
    {
        if (await _db.ApprovalGates.AnyAsync(g => g.OrganizationId == organizationId, ct))
            return;

        var gates = new[]
        {
            new ApprovalGate { OrganizationId = organizationId, GateType = ApprovalType.TechnicalReviewer, RequiredPermission = "tickets:approve:technical", SortOrder = 1 },
            new ApprovalGate { OrganizationId = organizationId, GateType = ApprovalType.Security, RequiredPermission = "tickets:approve:security", SortOrder = 2, IsEnabled = false },
            new ApprovalGate { OrganizationId = organizationId, GateType = ApprovalType.Database, RequiredPermission = "tickets:approve:database", SortOrder = 3, IsEnabled = false }
        };

        _db.ApprovalGates.AddRange(gates);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ApprovalGateDto>> GetGatesAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _db.ApprovalGates
            .Where(g => g.OrganizationId == organizationId)
            .OrderBy(g => g.SortOrder)
            .Select(g => new ApprovalGateDto(g.Id, g.GateType.ToString(), g.RequiredPermission, g.SortOrder, g.IsEnabled))
            .ToListAsync(ct);
    }

    public async Task<bool> AreAllGatesSatisfiedAsync(Guid ticketId, CancellationToken ct = default)
    {
        var pending = await GetPendingGateTypesAsync(ticketId, ct);
        return pending.Count == 0;
    }

    public async Task<List<string>> GetPendingGateTypesAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is null) return [];

        var gates = await _db.ApprovalGates
            .Where(g => g.OrganizationId == ticket.OrganizationId && g.IsEnabled)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(ct);

        if (gates.Count == 0)
            gates = [new ApprovalGate { GateType = ApprovalType.TechnicalReviewer }];

        var approvedTypes = await _db.Approvals
            .Where(a => a.TicketId == ticketId && a.Decision == ApprovalDecision.Approved)
            .Select(a => a.ApprovalType)
            .ToListAsync(ct);

        return gates
            .Where(g => !approvedTypes.Contains(g.GateType))
            .Select(g => g.GateType.ToString())
            .ToList();
    }
}
