using System.Diagnostics;
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
    private readonly IPlanLimitService _planLimits;

    public AiService(AppDbContext db, ITenantContext tenant, IAiProvider provider,
        IAiGovernanceService governance, IGraphService graph, IRagIndexService rag, IPlanLimitService planLimits)
    {
        _db = db;
        _tenant = tenant;
        _provider = provider;
        _governance = governance;
        _graph = graph;
        _rag = rag;
        _planLimits = planLimits;
    }

    public async Task<AiChatResponse> ChatAsync(Guid workspaceId, AiChatRequest request, Guid userId, CancellationToken ct = default)
    {
        using var activity = StackPilotTelemetry.StartActivity("ai.chat");
        activity?.SetTag("workspace.id", workspaceId);
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

        await _planLimits.EnsureCanUseAiAsync(_tenant.OrganizationId ?? Guid.Empty, ct: ct);

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
        using var activity = StackPilotTelemetry.StartActivity("ai.generate_requirements");
        activity?.SetTag("ticket.id", ticketId);
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var ragResults = await _rag.SearchAsync(ticket.WorkspaceId, $"{ticket.Title} {ticket.Description}", 10, ct);
        var citations = ragResults.Select(r => new AiCitationDto(r.GraphNodeId, r.Content[..Math.Min(200, r.Content.Length)])).ToList();
        var contextBlock = string.Join("\n", ragResults.Select((r, i) => $"[{i + 1}] (nodeId:{r.GraphNodeId}) {r.Content}"));

        await _planLimits.EnsureCanUseAiAsync(ticket.OrganizationId, ct: ct);

        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = """You are a business analyst AI. Generate structured requirements from a ticket using the provided system context. Return JSON with keys: businessSummary, functionalRequirements, nonFunctionalRequirements, acceptanceCriteria, riskScore (0-10), confidenceScore (0-1), citations (array of {nodeId, excerpt} referencing context items).""",
            UserPrompt = $"Title: {ticket.Title}\nDescription: {ticket.Description}\nBusiness Justification: {ticket.BusinessJustification}\nType: {ticket.TicketType}\n\nRelevant system context:\n{contextBlock}"
        }, ct);

        await _governance.RecordActionAsync("generate_requirements", ticket.Title, result.Content, result.Model, result.TokensUsed, true, ct);

        JsonElement parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<JsonElement>(result.Content);
        }
        catch
        {
            throw new InvalidOperationException("AI requirements output was not valid JSON");
        }

        if (parsed.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("AI requirements output must be a JSON object");

        string GetRequiredString(string name)
        {
            if (!parsed.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException($"AI requirements output missing required string field: {name}");

            var value = el.GetString();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"AI requirements output has empty field: {name}");

            return value;
        }

        decimal GetRequiredDecimal(string name)
        {
            if (!parsed.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
                throw new InvalidOperationException($"AI requirements output missing required numeric field: {name}");
            return el.GetDecimal();
        }

        // If the model provides citations, validate that they refer to retrieved nodes.
        var retrievedNodeIds = new HashSet<Guid>(ragResults.Where(r => r.GraphNodeId.HasValue).Select(r => r.GraphNodeId!.Value));
        if (parsed.TryGetProperty("citations", out var citationsEl) && citationsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in citationsEl.EnumerateArray())
            {
                if (!c.TryGetProperty("nodeId", out var nodeIdEl))
                    throw new InvalidOperationException("AI requirements output contains citations missing required nodeId");

                if (nodeIdEl.ValueKind != JsonValueKind.String)
                    throw new InvalidOperationException("AI requirements output citation nodeId must be a string GUID");

                var nodeIdStr = nodeIdEl.GetString();
                if (string.IsNullOrWhiteSpace(nodeIdStr) || !Guid.TryParse(nodeIdStr, out var nodeIdFromModel))
                    throw new InvalidOperationException("AI requirements output citation nodeId must be a valid GUID");

                // Only fail when we actually retrieved context items to cite against.
                if (retrievedNodeIds.Count > 0 && !retrievedNodeIds.Contains(nodeIdFromModel))
                    throw new InvalidOperationException("AI requirements output contains citations for nodes not present in retrieved context");

                if (!c.TryGetProperty("excerpt", out var excerptEl) ||
                    excerptEl.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(excerptEl.GetString()))
                    throw new InvalidOperationException("AI requirements output citation excerpt is required");
            }
        }

        var requirements = new AiRequirementsResult(
            GetRequiredString("businessSummary"),
            GetRequiredString("functionalRequirements"),
            GetRequiredString("nonFunctionalRequirements"),
            GetRequiredString("acceptanceCriteria"),
            GetRequiredDecimal("riskScore"),
            GetRequiredDecimal("confidenceScore"),
            citations);

        ticket.AiRequirementsJson = result.Content;
        ticket.RiskScore = requirements.RiskScore;
        ticket.ConfidenceScore = requirements.ConfidenceScore;
        ticket.Status = TicketStatus.AwaitingApproval;
        await _db.SaveChangesAsync(ct);

        return requirements;
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

        await _planLimits.EnsureCanUseAiAsync(ticket.OrganizationId, ct: ct);

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

        await _planLimits.EnsureCanUseAiAsync(page.OrganizationId, ct: ct);

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

    public async Task<AiCodeSuggestionDto> GenerateCodeAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (ticket.Status is not (TicketStatus.Approved or TicketStatus.ImplementationInProgress))
            throw new InvalidOperationException("Ticket must be approved before generating code");

        // Fail closed: when governance requires approval for code generation, require an explicit human-approved decision.
        if (await _governance.RequiresApprovalAsync("generate_code"))
        {
            var hasApproval = await _db.Approvals.AnyAsync(
                a => a.TicketId == ticketId && a.Decision == ApprovalDecision.Approved, ct);

            if (!hasApproval)
                throw new UnauthorizedAccessException("Human approval is required before AI code generation");
        }

        await _planLimits.EnsureCanUseAiAsync(ticket.OrganizationId, ct: ct);

        var suggestedCode = $$"""
            // StackPilot AI scaffold for ticket #{{ticket.TicketNumber}}: {{ticket.Title}}
            // TODO: Replace with production implementation

            public class Ticket{{ticket.TicketNumber}}Feature
            {
                public void Execute()
                {
                    // Implementation based on: {{ticket.Description ?? "requirements pending"}}
                }
            }
            """;

        var actionId = await _governance.RecordActionAsync(
            "generate_code", ticket.Title, suggestedCode, "scaffold", 500, true, ct);

        return new AiCodeSuggestionDto(actionId, suggestedCode, "csharp", $"Scaffold for {ticket.Title}");
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
