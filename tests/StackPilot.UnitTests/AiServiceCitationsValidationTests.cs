using System.Text.Json;
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

public class AiServiceCitationsValidationTests
{
    [Fact]
    public async Task GenerateRequirementsAsync_Throws_WhenOutput_IsNotValidJson()
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
            Status = TicketStatus.AiAnalysisPending,
            BusinessJustification = "justification",
            RequesterId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var ragNodeId = Guid.NewGuid();
        var rag = new FakeRagIndexService([new RagSearchResult(ragNodeId, "ctx", 0.1)]);
        var provider = new FakeAiProvider("this is not json");
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var graph = new FakeGraphService();

        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateRequirementsAsync(ticketId, CancellationToken.None));
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task GenerateRequirementsAsync_Throws_WhenOutput_MissingRequiredField()
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
            Status = TicketStatus.AiAnalysisPending,
            BusinessJustification = "justification",
            RequesterId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var ragNodeId = Guid.NewGuid();
        var rag = new FakeRagIndexService([new RagSearchResult(ragNodeId, "ctx", 0.1)]);
        var providerJson = JsonSerializer.Serialize(new
        {
            businessSummary = "biz",
            functionalRequirements = "func",
            nonFunctionalRequirements = "nfr",
            riskScore = 4.5m,
            confidenceScore = 0.8m,
            citations = new[]
            {
                new { nodeId = ragNodeId.ToString(), excerpt = "excerpt" }
            }
        });

        var provider = new FakeAiProvider(providerJson);
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var graph = new FakeGraphService();

        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateRequirementsAsync(ticketId, CancellationToken.None));
        Assert.Contains("acceptanceCriteria", ex.Message);
    }

    [Fact]
    public async Task GenerateRequirementsAsync_Throws_WhenCitationNodeId_IsNotGuid()
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
            Status = TicketStatus.AiAnalysisPending,
            BusinessJustification = "justification",
            RequesterId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var ragNodeId = Guid.NewGuid();
        var rag = new FakeRagIndexService([new RagSearchResult(ragNodeId, "ctx", 0.1)]);
        var provider = new FakeAiProvider(ValidRequirementsJsonWithCitations(
            nodeId: "not-a-guid",
            excerpt: "excerpt"));
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var graph = new FakeGraphService();

        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateRequirementsAsync(ticketId, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateRequirementsAsync_Throws_WhenCitationNodeId_NotInRetrievedContext()
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
            Status = TicketStatus.AiAnalysisPending,
            BusinessJustification = "justification",
            RequesterId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var retrievedNodeId = Guid.NewGuid();
        var otherNodeId = Guid.NewGuid();

        var rag = new FakeRagIndexService([new RagSearchResult(retrievedNodeId, "ctx", 0.1)]);
        var provider = new FakeAiProvider(ValidRequirementsJsonWithCitations(
            nodeId: otherNodeId.ToString(),
            excerpt: "excerpt"));
        var governance = new FakeGovernanceService();
        var planLimits = new FakePlanLimitService();
        var graph = new FakeGraphService();

        var tenant = new TenantContext();
        tenant.SetOrganization(orgId);

        var service = new AiService(db, tenant, provider, governance, graph, rag, planLimits);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateRequirementsAsync(ticketId, CancellationToken.None));
    }

    private static string ValidRequirementsJsonWithCitations(string nodeId, string excerpt) =>
        $$"""
          {
            "businessSummary": "biz",
            "functionalRequirements": "func",
            "nonFunctionalRequirements": "nfr",
            "acceptanceCriteria": "ac",
            "riskScore": 4.5,
            "confidenceScore": 0.8,
            "citations": [
              { "nodeId": "{{nodeId}}", "excerpt": "{{excerpt}}" }
            ]
          }
          """;

    private static AppDbContext CreateDb(Guid orgId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AiServiceCitationsValidation_" + Guid.NewGuid())
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
        public Task<ImpactAnalysisDto> AnalyzeImpactAsync(Guid nodeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphNodeDto>> GetApplicationsAsync(Guid workspaceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<GraphNodeDto?> GetNodeAsync(Guid nodeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PagedResult<GraphNodeDto>> GetNodesAsync(Guid workspaceId, PagedRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphEdgeDto>> GetEdgesAsync(Guid workspaceId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<GraphNodeDto>> SearchAsync(Guid workspaceId, GraphSearchRequest request, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private class FakeRagIndexService : IRagIndexService
    {
        private readonly List<RagSearchResult> _results;

        public FakeRagIndexService(List<RagSearchResult> results) => _results = results;

        public Task IndexRepositoryScanAsync(Guid organizationId, Guid workspaceId, Guid? graphNodeId, string repositoryName, string scanResultsJson, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<List<RagSearchResult>> SearchAsync(Guid workspaceId, string query, int topK = 10, CancellationToken ct = default) =>
            Task.FromResult(_results);
    }
}

