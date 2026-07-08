using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Billing;
using StackPilot.Application.Common;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;
using StackPilot.Infrastructure.Services;

namespace StackPilot.UnitTests;

public class PlanLimitServiceTests
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
    public async Task EnsureCanCreateConnector_ThrowsWhenAtTrialLimit()
    {
        await using var db = CreateDb(nameof(EnsureCanCreateConnector_ThrowsWhenAtTrialLimit));
        var org = new Organization { Name = "Trial Org", Slug = "trial", Plan = OrganizationPlan.Trial };
        db.Organizations.Add(org);
        for (var i = 0; i < 3; i++)
        {
            db.ConnectorInstances.Add(new ConnectorInstance
            {
                OrganizationId = org.Id,
                WorkspaceId = Guid.NewGuid(),
                DefinitionId = Guid.NewGuid(),
                Name = $"C{i}",
                ConfigJson = "{}"
            });
        }
        await db.SaveChangesAsync();

        var service = new PlanLimitService(db);
        var ex = await Assert.ThrowsAsync<PlanLimitException>(() =>
            service.EnsureCanCreateConnectorAsync(org.Id));
        Assert.Equal("CONNECTOR_LIMIT", ex.LimitCode);
    }

    [Fact]
    public async Task GetEnforcementStatus_BlocksExpiredTrial()
    {
        await using var db = CreateDb(nameof(GetEnforcementStatus_BlocksExpiredTrial));
        var org = new Organization
        {
            Name = "Expired",
            Slug = "expired",
            Plan = OrganizationPlan.Trial,
            TrialEndsAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var service = new PlanLimitService(db);
        var status = await service.GetEnforcementStatusAsync(org.Id);
        Assert.True(status.IsWriteBlocked);
        Assert.Contains("trial", status.BlockReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Canceled, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Active, false)]
    public async Task GetEnforcementStatus_ReflectsSubscriptionStatus(SubscriptionStatus subscriptionStatus, bool blocked)
    {
        await using var db = CreateDb(nameof(GetEnforcementStatus_ReflectsSubscriptionStatus) + subscriptionStatus);
        var org = new Organization
        {
            Name = "Sub Org",
            Slug = $"sub-{subscriptionStatus}",
            Plan = OrganizationPlan.Starter,
            SubscriptionStatus = subscriptionStatus
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var service = new PlanLimitService(db);
        var status = await service.GetEnforcementStatusAsync(org.Id);
        Assert.Equal(blocked, status.IsWriteBlocked);
    }

    [Fact]
    public async Task EnsureCanUseAi_ThrowsWhenTokenBudgetExceeded()
    {
        await using var db = CreateDb(nameof(EnsureCanUseAi_ThrowsWhenTokenBudgetExceeded));
        var org = new Organization { Name = "AI Org", Slug = "ai", Plan = OrganizationPlan.Trial };
        db.Organizations.Add(org);
        db.AiActions.Add(new AiAction
        {
            OrganizationId = org.Id,
            ActionType = "requirements",
            TokensUsed = 100_000,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new PlanLimitService(db);
        var ex = await Assert.ThrowsAsync<PlanLimitException>(() =>
            service.EnsureCanUseAiAsync(org.Id));
        Assert.Equal("AI_TOKEN_LIMIT", ex.LimitCode);
    }
}
