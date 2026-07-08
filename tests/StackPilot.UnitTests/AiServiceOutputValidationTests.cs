using Microsoft.EntityFrameworkCore;
using StackPilot.Application.AI;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;
using StackPilot.Infrastructure.Services;

namespace StackPilot.UnitTests;

public class AiServiceOutputValidationTests
{
    [Fact]
    public async Task GenerateImplementationPlanAsync_Throws_When_MissingRequiredHeading()
    {
        var orgId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        using var db = CreateDb(orgId);
        db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            OrganizationId = orgId,
            WorkspaceId = workspaceId,
            TicketNumber = 1,
            Title = "t",
            Description = "d",
            TicketType = TicketType.Enhancement,
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Approved,
            RequesterId = Guid.NewGuid(),
            BusinessJustification = "justification",
            AiRequirementsJson = "{}"
        });
        await db.SaveChangesAsync();

        var rag = new FakeRagIndexService();
        var graph = new FakeGraphService();
        var provider = new FakeAiProvider("## Implementation Plan\n\n### Affected Components\n- X\n\n### Steps\n1. Do it\n\n"); // missing rollback section
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateImplementationPlanAsync(ticketId, CancellationToken.None));

        Assert.Contains("missing required section", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateImplementationPlanAsync_Succeeds_When_RequiredHeadingsPresent()
    {
        var orgId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        using var db = CreateDb(orgId);
        db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            OrganizationId = orgId,
            WorkspaceId = workspaceId,
            TicketNumber = 1,
            Title = "t",
            Description = "d",
            TicketType = TicketType.Enhancement,
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Approved,
            RequesterId = Guid.NewGuid(),
            BusinessJustification = "justification",
            AiRequirementsJson = "{}"
        });
        await db.SaveChangesAsync();

        var rag = new FakeRagIndexService();
        var graph = new FakeGraphService();
        var plan = "## Implementation Plan\n\n### Affected Components\n- Service layer\n\n### Steps\n1. Do it\n\n### Rollback Plan\nUndo it";
        var provider = new FakeAiProvider(plan);
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        var content = await service.GenerateImplementationPlanAsync(ticketId, CancellationToken.None);
        Assert.Equal(plan, content);

        var saved = await db.Tickets.FirstAsync(t => t.Id == ticketId);
        Assert.Equal(TicketStatus.ImplementationInProgress, saved.Status);
        Assert.Equal(plan, saved.ImplementationPlanJson);
    }

    [Fact]
    public async Task GenerateDocumentationAsync_Throws_When_NoMarkdownHeading()
    {
        var orgId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var pageId = Guid.NewGuid();

        using var db = CreateDb(orgId);
        db.DocumentationPages.Add(new DocumentationPage
        {
            Id = pageId,
            OrganizationId = orgId,
            WorkspaceId = workspaceId,
            Title = "Doc title",
            DocType = "Architecture"
        });
        await db.SaveChangesAsync();

        var provider = new FakeAiProvider("plain text without markdown headings");
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, new FakeGraphService(), new FakeRagIndexService(), planLimits);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateDocumentationAsync(pageId, CancellationToken.None));

        Assert.Contains("must contain at least one markdown heading", ex.Message);
        Assert.Equal(0, await db.DocumentationVersions.CountAsync(v => v.PageId == pageId));
    }

    [Fact]
    public async Task GenerateDocumentationAsync_Succeeds_When_MarkdownHasHeading()
    {
        var orgId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var pageId = Guid.NewGuid();

        using var db = CreateDb(orgId);
        db.DocumentationPages.Add(new DocumentationPage
        {
            Id = pageId,
            OrganizationId = orgId,
            WorkspaceId = workspaceId,
            Title = "Doc title",
            DocType = "Architecture"
        });
        await db.SaveChangesAsync();

        var content = "# Documentation\n\n## Overview\nHello";
        var provider = new FakeAiProvider(content);
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, new FakeGraphService(), new FakeRagIndexService(), planLimits);

        var result = await service.GenerateDocumentationAsync(pageId, CancellationToken.None);
        Assert.Equal(content, result);

        var versions = await db.DocumentationVersions.Where(v => v.PageId == pageId).ToListAsync();
        Assert.Single(versions);
        Assert.Equal(content, versions[0].ContentMd);
        Assert.Equal("ai", versions[0].GeneratedBy);
        Assert.Equal(DocumentationStatus.Draft, versions[0].Status);
    }

    private static AppDbContext CreateDb(Guid orgId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AiServiceOutputValidation_" + Guid.NewGuid())
            .Options;

        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        return new AppDbContext(options, tenant);
    }

    private class FakeAiProvider : IAiProvider
    {
        private readonly string _content;

        public FakeAiProvider(string content) => _content = content;

        public string ProviderName => "fake";

        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default) =>
            Task.FromResult(new AiCompletionResult { Content = _content, Model = "fake", TokensUsed = 1 });

        public Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default) =>
            Task.FromResult(new AiEmbeddingResult());
    }

    private class FakeGovernanceService : IAiGovernanceService
    {
        public Task<Guid> RecordActionAsync(string actionType, string? input, string? output, string model, int tokens, bool isReversible, CancellationToken ct = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<bool> RequiresApprovalAsync(string actionType) => Task.FromResult(false);
    }

    private class FakePlanLimitService : IPlanLimitService
    {
        public Task EnsureCanCreateConnectorAsync(Guid organizationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanCreateWorkspaceAsync(Guid organizationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanUseAiAsync(Guid organizationId, int estimatedTokens = 4_000, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanAddSeatAsync(Guid organizationId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PlanEnforcementDto> GetEnforcementStatusAsync(Guid organizationId, CancellationToken ct = default) =>
            Task.FromResult(new PlanEnforcementDto(false, null, false, false, false, false));
    }

    private class FakeGraphService : IGraphService
    {
        public Task<PagedResult<GraphNodeDto>> GetNodesAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<GraphNodeDto?> GetNodeAsync(Guid nodeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphEdgeDto>> GetEdgesAsync(Guid workspaceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphNodeDto>> SearchAsync(Guid workspaceId, GraphSearchRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<ImpactAnalysisDto> AnalyzeImpactAsync(Guid nodeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphNodeDto>> GetApplicationsAsync(Guid workspaceId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeRagIndexService : IRagIndexService
    {
        public Task IndexRepositoryScanAsync(Guid organizationId, Guid workspaceId, Guid? graphNodeId, string repositoryName, string scanResultsJson, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<List<RagSearchResult>> SearchAsync(Guid workspaceId, string query, int topK = 10, CancellationToken ct = default) =>
            Task.FromResult(new List<RagSearchResult>());
    }
}

