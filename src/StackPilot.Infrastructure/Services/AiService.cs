using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.AI;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class AiService : IAiService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAiProvider _provider;
    private readonly IAiGovernanceService _governance;
    private readonly IGraphService _graph;
    private readonly IRagIndexService _rag;

    public AiService(AppDbContext db, ITenantContext tenant, IAiProvider provider,
        IAiGovernanceService governance, IGraphService graph, IRagIndexService rag)
    {
        _db = db;
        _tenant = tenant;
        _provider = provider;
        _governance = governance;
        _graph = graph;
        _rag = rag;
    }

    public async Task<AiChatResponse> ChatAsync(Guid workspaceId, AiChatRequest request, Guid userId, CancellationToken ct = default)
    {
        AiConversation conversation;
        if (request.ConversationId.HasValue)
        {
            conversation = await _db.AiConversations.FindAsync([request.ConversationId.Value], ct)
                ?? throw new KeyNotFoundException("Conversation not found");
        }
        else
        {
            conversation = new AiConversation
            {
                OrganizationId = _tenant.OrganizationId ?? Guid.Empty,
                WorkspaceId = workspaceId,
                UserId = userId,
                Title = request.Message.Length > 50 ? request.Message[..50] + "..." : request.Message
            };
            _db.AiConversations.Add(conversation);
        }

        var ragResults = await _rag.SearchAsync(workspaceId, request.Message, 5, ct);
        var graphContext = await _graph.SearchAsync(workspaceId, new GraphSearchRequest(request.Message, null, 5), ct);
        var contextStr = string.Join("\n", ragResults.Select(r => $"- [node:{r.GraphNodeId}] {r.Content}")
            .Concat(graphContext.Select(n => $"- {n.NodeType}: {n.Name}")));

        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = """You are StackPilot AI, an enterprise software intelligence assistant. Answer questions about the user's software ecosystem using the provided knowledge graph context. Be precise, cite affected systems, and highlight risks.""",
            UserPrompt = $"Context from knowledge graph:\n{contextStr}\n\nUser question: {request.Message}"
        }, ct);

        await _governance.RecordActionAsync("chat", request.Message, result.Content, result.Model, result.TokensUsed, true, ct);

        return new AiChatResponse(result.Content, conversation.Id, graphContext.Select(n => n.Name).ToArray());
    }

    public async Task<AiRequirementsResult> GenerateRequirementsAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var ragResults = await _rag.SearchAsync(ticket.WorkspaceId, $"{ticket.Title} {ticket.Description}", 10, ct);
        var citations = ragResults.Select(r => new AiCitationDto(r.GraphNodeId, r.Content[..Math.Min(200, r.Content.Length)])).ToList();
        var contextBlock = string.Join("\n", ragResults.Select((r, i) => $"[{i + 1}] (nodeId:{r.GraphNodeId}) {r.Content}"));

        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = """You are a business analyst AI. Generate structured requirements from a ticket using the provided system context. Return JSON with keys: businessSummary, functionalRequirements, nonFunctionalRequirements, acceptanceCriteria, riskScore (0-10), confidenceScore (0-1), citations (array of {nodeId, excerpt} referencing context items).""",
            UserPrompt = $"Title: {ticket.Title}\nDescription: {ticket.Description}\nBusiness Justification: {ticket.BusinessJustification}\nType: {ticket.TicketType}\n\nRelevant system context:\n{contextBlock}"
        }, ct);

        await _governance.RecordActionAsync("generate_requirements", ticket.Title, result.Content, result.Model, result.TokensUsed, true, ct);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(result.Content);
            var requirements = new AiRequirementsResult(
                parsed.GetProperty("businessSummary").GetString() ?? "",
                parsed.GetProperty("functionalRequirements").GetString() ?? "",
                parsed.GetProperty("nonFunctionalRequirements").GetString() ?? "",
                parsed.GetProperty("acceptanceCriteria").GetString() ?? "",
                parsed.TryGetProperty("riskScore", out var rs) ? rs.GetDecimal() : 5m,
                parsed.TryGetProperty("confidenceScore", out var cs) ? cs.GetDecimal() : 0.7m,
                citations);

            ticket.AiRequirementsJson = result.Content;
            ticket.RiskScore = requirements.RiskScore;
            ticket.ConfidenceScore = requirements.ConfidenceScore;
            ticket.Status = TicketStatus.AwaitingApproval;
            await _db.SaveChangesAsync(ct);

            return requirements;
        }
        catch
        {
            var fallback = new AiRequirementsResult(
                $"Business need: {ticket.Title}",
                ticket.Description ?? "To be defined",
                "Standard non-functional requirements apply",
                "- Feature works as described\n- No regression in existing functionality",
                5m, 0.6m, citations);

            ticket.AiRequirementsJson = JsonSerializer.Serialize(fallback);
            ticket.Status = TicketStatus.AwaitingApproval;
            ticket.RiskScore = 5m;
            ticket.ConfidenceScore = 0.6m;
            await _db.SaveChangesAsync(ct);
            return fallback;
        }
    }

    public async Task<string> GenerateImplementationPlanAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (ticket.Status != TicketStatus.Approved)
            throw new UnauthorizedAccessException("Ticket must be approved before generating an implementation plan");

        if (await _governance.RequiresApprovalAsync("generate_plan"))
        {
            var hasApproval = await _db.Approvals.AnyAsync(a =>
                a.TicketId == ticketId && a.Decision == ApprovalDecision.Approved, ct);
            if (!hasApproval)
                throw new UnauthorizedAccessException("Human approval is required before AI plan generation");
        }

        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = """You are a senior architect AI. Generate a detailed technical implementation plan including affected files, APIs, database changes, test plan, security considerations, and rollback plan. Format as structured markdown.""",
            UserPrompt = $"Ticket: {ticket.Title}\nRequirements: {ticket.AiRequirementsJson}\nDescription: {ticket.Description}"
        }, ct);

        await _governance.RecordActionAsync("generate_plan", ticket.Title, result.Content, result.Model, result.TokensUsed, true, ct);

        ticket.ImplementationPlanJson = result.Content;
        ticket.Status = TicketStatus.ImplementationInProgress;
        await _db.SaveChangesAsync(ct);
        return result.Content;
    }

    public async Task<string> GenerateDocumentationAsync(Guid pageId, CancellationToken ct = default)
    {
        var page = await _db.DocumentationPages.FindAsync([pageId], ct)
            ?? throw new KeyNotFoundException("Documentation page not found");

        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = "You are a technical documentation AI. Generate comprehensive, well-structured markdown documentation.",
            UserPrompt = $"Generate {page.DocType} documentation for: {page.Title}"
        }, ct);

        await _governance.RecordActionAsync("generate_documentation", page.Title, result.Content, result.Model, result.TokensUsed, true, ct);

        var latestVersion = await _db.DocumentationVersions
            .Where(v => v.PageId == pageId)
            .MaxAsync(v => (int?)v.Version, ct) ?? 0;

        _db.DocumentationVersions.Add(new DocumentationVersion
        {
            PageId = pageId,
            Version = latestVersion + 1,
            ContentMd = result.Content,
            GeneratedBy = "ai",
            Status = DocumentationStatus.Draft
        });
        await _db.SaveChangesAsync(ct);
        return result.Content;
    }
}

public class AiGovernanceService : IAiGovernanceService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    private static readonly HashSet<string> ApprovalRequired = new(StringComparer.OrdinalIgnoreCase)
    {
        "generate_code", "create_branch", "write_file", "create_migration", "create_pr", "trigger_build", "deploy", "generate_plan"
    };

    public AiGovernanceService(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> RecordActionAsync(string actionType, string? input, string? output, string model, int tokens, bool isReversible, CancellationToken ct = default)
    {
        var action = new AiAction
        {
            OrganizationId = _tenant.OrganizationId ?? Guid.Empty,
            UserId = _tenant.UserId,
            ActionType = actionType,
            InputJson = input,
            OutputJson = output,
            Model = model,
            TokensUsed = tokens,
            Status = AiActionStatus.Completed,
            IsReversible = isReversible
        };
        _db.AiActions.Add(action);
        await _db.SaveChangesAsync(ct);
        return action.Id;
    }

    public Task<bool> RequiresApprovalAsync(string actionType) =>
        Task.FromResult(ApprovalRequired.Contains(actionType));
}
