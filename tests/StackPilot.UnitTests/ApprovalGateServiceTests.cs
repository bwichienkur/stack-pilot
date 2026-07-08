using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;
using StackPilot.Infrastructure.Security;

namespace StackPilot.UnitTests;

public class ApprovalGateServiceTests
{
    private static AppDbContext CreateDb(string dbName)
    {
        var tenant = new TenantContext();
        tenant.DisableTenantFilter();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options, tenant);
    }

    [Fact]
    public async Task EnsureDefaultGates_CreatesThreeGates()
    {
        await using var db = CreateDb(nameof(EnsureDefaultGates_CreatesThreeGates));
        var orgId = Guid.NewGuid();
        var service = new ApprovalGateService(db);

        await service.EnsureDefaultGatesAsync(orgId);

        var gates = await db.ApprovalGates.Where(g => g.OrganizationId == orgId).ToListAsync();
        Assert.Equal(3, gates.Count);
        Assert.Single(gates, g => g.GateType == ApprovalType.TechnicalReviewer && g.IsEnabled);
        Assert.Equal(2, gates.Count(g => !g.IsEnabled));
    }

    [Fact]
    public async Task GetPendingGateTypes_ReturnsTechnicalWhenNoApproval()
    {
        await using var db = CreateDb(nameof(GetPendingGateTypes_ReturnsTechnicalWhenNoApproval));
        var orgId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            OrganizationId = orgId,
            WorkspaceId = Guid.NewGuid(),
            Title = "Test",
            TicketType = TicketType.Bug,
            RequesterId = Guid.NewGuid(),
            Status = TicketStatus.AwaitingApproval
        });
        db.ApprovalGates.Add(new ApprovalGate
        {
            OrganizationId = orgId,
            GateType = ApprovalType.TechnicalReviewer,
            RequiredPermission = "tickets:approve:technical",
            SortOrder = 1,
            IsEnabled = true
        });
        await db.SaveChangesAsync();

        var service = new ApprovalGateService(db);
        var pending = await service.GetPendingGateTypesAsync(ticketId);
        Assert.Contains(nameof(ApprovalType.TechnicalReviewer), pending);
    }

    [Fact]
    public async Task AreAllGatesSatisfied_ReturnsTrueAfterTechnicalApproval()
    {
        await using var db = CreateDb(nameof(AreAllGatesSatisfied_ReturnsTrueAfterTechnicalApproval));
        var orgId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            OrganizationId = orgId,
            WorkspaceId = Guid.NewGuid(),
            Title = "Test",
            TicketType = TicketType.Bug,
            RequesterId = Guid.NewGuid(),
            Status = TicketStatus.AwaitingApproval
        });
        db.ApprovalGates.Add(new ApprovalGate
        {
            OrganizationId = orgId,
            GateType = ApprovalType.TechnicalReviewer,
            RequiredPermission = "tickets:approve:technical",
            SortOrder = 1,
            IsEnabled = true
        });
        db.Approvals.Add(new Approval
        {
            OrganizationId = orgId,
            TicketId = ticketId,
            ApprovalType = ApprovalType.TechnicalReviewer,
            ApproverId = approverId,
            Decision = ApprovalDecision.Approved
        });
        await db.SaveChangesAsync();

        var service = new ApprovalGateService(db);
        Assert.True(await service.AreAllGatesSatisfiedAsync(ticketId));
    }
}
