using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.AI;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.External;
using StackPilot.Infrastructure.Persistence;
using StackPilot.Infrastructure.Security;

namespace StackPilot.Infrastructure.Services;

public class AiWorkflowService : IAiWorkflowService
{
    private static readonly HashSet<string> SupportedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_branch", "write_file", "create_migration", "create_pr", "trigger_build", "deploy"
    };

    private readonly AppDbContext _db;
    private readonly IAiGovernanceService _governance;
    private readonly IAiProvider _provider;
    private readonly IGitHubApiClient _github;
    private readonly ICredentialEncryptionService _encryption;
    private readonly IPlanLimitService _planLimits;

    public AiWorkflowService(
        AppDbContext db,
        IAiGovernanceService governance,
        IAiProvider provider,
        IGitHubApiClient github,
        ICredentialEncryptionService encryption,
        IPlanLimitService planLimits)
    {
        _db = db;
        _governance = governance;
        _provider = provider;
        _github = github;
        _encryption = encryption;
        _planLimits = planLimits;
    }

    public async Task<AiWorkflowActionResultDto> ExecuteAsync(
        Guid ticketId, string actionType, ExecuteAiWorkflowActionRequest request, CancellationToken ct = default)
    {
        if (!SupportedActions.Contains(actionType))
            throw new ArgumentException($"Unsupported AI workflow action: {actionType}");

        var ticket = await _db.Tickets.FindAsync([ticketId], ct)
            ?? throw new KeyNotFoundException("Ticket not found");

        var isDeploy = actionType.Equals("deploy", StringComparison.OrdinalIgnoreCase);
        if (!isDeploy && ticket.Status is not (TicketStatus.Approved or TicketStatus.ImplementationInProgress or TicketStatus.PullRequestCreated))
            throw new InvalidOperationException("Ticket must be approved before executing AI workflow actions");

        await _governance.EnsureTicketApprovalAsync(ticketId, actionType, ct);
        await _planLimits.EnsureCanUseAiAsync(ticket.OrganizationId, ct: ct);

        return actionType.ToLowerInvariant() switch
        {
            "create_branch" => await CreateBranchAsync(ticket, request, ct),
            "write_file" => await WriteFileAsync(ticket, request, ct),
            "create_migration" => await CreateMigrationAsync(ticket, request, ct),
            "create_pr" => await CreatePullRequestAsync(ticket, request, ct),
            "trigger_build" => await TriggerBuildAsync(ticket, request, ct),
            "deploy" => await DeployAsync(ticket, ct),
            _ => throw new ArgumentException($"Unsupported AI workflow action: {actionType}")
        };
    }

    public async Task<AiActionReversalResultDto> ReverseActionAsync(Guid actionId, CancellationToken ct = default)
    {
        var result = await _governance.ReverseActionAsync(actionId, ct);
        return new AiActionReversalResultDto(result.OriginalActionId, result.ReversalActionId, result.Message);
    }

    private async Task<AiWorkflowActionResultDto> CreateBranchAsync(Ticket ticket, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        var (owner, repo, token, defaultBranch) = await ResolveGitHubAsync(ticket.WorkspaceId, request, ct);
        var branchName = request.BranchName ?? $"stackpilot/ticket-{ticket.TicketNumber}";
        var fromBranch = defaultBranch ?? "main";

        if (token is null)
        {
            var actionId = await _governance.RecordActionAsync(
                "create_branch", ticket.Title, $"branch:{branchName}", "mock", 0, true, ct);
            return new AiWorkflowActionResultDto(actionId, "create_branch", true,
                $"Simulated branch '{branchName}' (connect GitHub to create a real branch)",
                JsonSerializer.Serialize(new { branchName, owner, repo, simulated = true }));
        }

        var created = await _github.CreateBranchAsync(token, owner, repo, branchName, fromBranch, ct);
        if (created is null)
            throw new InvalidOperationException($"Failed to create branch '{branchName}' in {owner}/{repo}");

        var output = JsonSerializer.Serialize(new { branchName, owner, repo, @ref = created.Ref });
        var id = await _governance.RecordActionAsync("create_branch", ticket.Title, output, "github", 0, true, ct);
        ticket.Status = TicketStatus.ImplementationInProgress;
        await _db.SaveChangesAsync(ct);
        return new AiWorkflowActionResultDto(id, "create_branch", true, $"Created branch {branchName}", output);
    }

    private async Task<AiWorkflowActionResultDto> WriteFileAsync(Ticket ticket, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.FileContent))
            throw new ArgumentException("FilePath and FileContent are required for write_file");

        var (owner, repo, token, defaultBranch) = await ResolveGitHubAsync(ticket.WorkspaceId, request, ct);
        var branchName = request.BranchName ?? $"stackpilot/ticket-{ticket.TicketNumber}";

        if (token is null)
        {
            var actionId = await _governance.RecordActionAsync(
                "write_file", request.FilePath, request.FileContent, "mock", 0, true, ct);
            return new AiWorkflowActionResultDto(actionId, "write_file", true,
                $"Simulated write to {request.FilePath}",
                JsonSerializer.Serialize(new { path = request.FilePath, branchName, simulated = true }));
        }

        var commit = await _github.CreateOrUpdateFileAsync(
            token, owner, repo, request.FilePath, branchName, request.FileContent,
            $"StackPilot: ticket #{ticket.TicketNumber} — {ticket.Title}", ct);
        if (commit is null)
            throw new InvalidOperationException($"Failed to write file '{request.FilePath}' to {owner}/{repo}");

        var output = JsonSerializer.Serialize(new
        {
            path = request.FilePath,
            branchName,
            commitSha = commit.Commit?.Sha,
            url = commit.Content?.HtmlUrl
        });
        var id = await _governance.RecordActionAsync("write_file", request.FilePath, output, "github", 0, true, ct);
        return new AiWorkflowActionResultDto(id, "write_file", true, $"Committed {request.FilePath}", output);
    }

    private async Task<AiWorkflowActionResultDto> CreateMigrationAsync(Ticket ticket, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        var migrationName = request.MigrationName ?? $"Ticket{ticket.TicketNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var result = await _provider.CompleteAsync(new AiCompletionRequest
        {
            SystemPrompt = """You are a database migration expert. Generate a safe, idempotent SQL migration script. Return JSON with keys: summary (string), upSql (string), downSql (string), warnings (array of strings).""",
            UserPrompt = $"Ticket #{ticket.TicketNumber}: {ticket.Title}\nRequirements:\n{ticket.AiRequirementsJson}\nPlan:\n{ticket.ImplementationPlanJson}"
        }, ct);

        ValidateMigrationOutput(result.Content);
        var filePath = $"migrations/{migrationName}.sql";
        var actionId = await _governance.RecordActionAsync("create_migration", migrationName, result.Content, result.Model, result.TokensUsed, true, ct);
        return new AiWorkflowActionResultDto(actionId, "create_migration", true,
            $"Generated migration {migrationName}",
            JsonSerializer.Serialize(new { migrationName, filePath, content = result.Content }));
    }

    private async Task<AiWorkflowActionResultDto> CreatePullRequestAsync(Ticket ticket, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        var (owner, repo, token, defaultBranch) = await ResolveGitHubAsync(ticket.WorkspaceId, request, ct);
        var headBranch = request.BranchName ?? $"stackpilot/ticket-{ticket.TicketNumber}";
        var baseBranch = defaultBranch ?? "main";
        var title = request.PullRequestTitle ?? $"[{ticket.TicketNumber}] {ticket.Title}";
        var body = request.PullRequestBody ?? ticket.ImplementationPlanJson ?? ticket.Description;

        if (token is null)
        {
            var actionId = await _governance.RecordActionAsync(
                "create_pr", title, body, "mock", 0, true, ct);
            return new AiWorkflowActionResultDto(actionId, "create_pr", true,
                $"Simulated pull request from {headBranch}",
                JsonSerializer.Serialize(new { title, headBranch, baseBranch, simulated = true }));
        }

        var pr = await _github.CreatePullRequestAsync(token, owner, repo, title, headBranch, baseBranch, body, ct);
        if (pr is null)
            throw new InvalidOperationException($"Failed to create pull request in {owner}/{repo}");

        var output = JsonSerializer.Serialize(new { pr.Number, pr.HtmlUrl, title, headBranch, baseBranch });
        var id = await _governance.RecordActionAsync("create_pr", title, output, "github", 0, true, ct);
        ticket.Status = TicketStatus.PullRequestCreated;
        await _db.SaveChangesAsync(ct);
        return new AiWorkflowActionResultDto(id, "create_pr", true, $"Opened PR #{pr.Number}", output);
    }

    private async Task<AiWorkflowActionResultDto> TriggerBuildAsync(Ticket ticket, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        var (owner, repo, token, _) = await ResolveGitHubAsync(ticket.WorkspaceId, request, ct);
        if (token is null)
        {
            var actionId = await _governance.RecordActionAsync(
                "trigger_build", ticket.Title, null, "mock", 0, false, ct);
            return new AiWorkflowActionResultDto(actionId, "trigger_build", true,
                "Simulated CI build trigger (connect GitHub Actions for live builds)");
        }

        var runs = await _github.ListWorkflowRunsAsync(token, owner, repo, 1, ct);
        var output = JsonSerializer.Serialize(new { owner, repo, latestRun = runs.FirstOrDefault()?.HtmlUrl });
        var id = await _governance.RecordActionAsync("trigger_build", ticket.Title, output, "github", 0, false, ct);
        ticket.Status = TicketStatus.BuildRunning;
        await _db.SaveChangesAsync(ct);
        return new AiWorkflowActionResultDto(id, "trigger_build", true,
            runs.Count > 0 ? "Latest workflow run linked to ticket" : "No workflow runs found yet; push a branch to trigger CI",
            output);
    }

    private async Task<AiWorkflowActionResultDto> DeployAsync(Ticket ticket, CancellationToken ct)
    {
        if (ticket.Status is not (TicketStatus.UatAccepted or TicketStatus.ScheduledForProduction or TicketStatus.DeployedToProduction))
        {
            return new AiWorkflowActionResultDto(
                Guid.Empty, "deploy", false,
                "Deploy requires UAT acceptance. Complete QA/UAT workflow before scheduling production release.");
        }

        var output = JsonSerializer.Serialize(new
        {
            ticketId = ticket.Id,
            message = "Use POST /tickets/{id}/schedule-release to schedule a governed production deployment"
        });
        var id = await _governance.RecordActionAsync("deploy", ticket.Title, output, "governance", 0, true, ct);
        return new AiWorkflowActionResultDto(id, "deploy", true,
            "Deployment must be scheduled via the release workflow with human approval", output);
    }

    private async Task<(string Owner, string Repo, string? Token, string? DefaultBranch)> ResolveGitHubAsync(
        Guid workspaceId, ExecuteAiWorkflowActionRequest request, CancellationToken ct)
    {
        var query = _db.ConnectorInstances
            .Include(c => c.Definition)
            .Include(c => c.Credentials)
            .Where(c => c.WorkspaceId == workspaceId && c.Definition.Type == "github_repository");

        var instance = request.ConnectorId.HasValue
            ? await query.FirstOrDefaultAsync(c => c.Id == request.ConnectorId.Value, ct)
            : await query.OrderByDescending(c => c.LastSyncAt).FirstOrDefaultAsync(ct);

        if (instance is null)
            return (request.RepositoryOwner ?? "owner", request.RepositoryName ?? "repo", null, "main");

        var credentials = instance.Credentials.ToDictionary(
            c => c.CredentialType,
            c => _encryption.Decrypt(c.EncryptedValue, instance.OrganizationId));
        var token = credentials.GetValueOrDefault("pat") ?? credentials.GetValueOrDefault("oauth_token");

        var config = string.IsNullOrWhiteSpace(instance.ConfigJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(instance.ConfigJson) ?? new();

        var owner = request.RepositoryOwner ?? config.GetValueOrDefault("owner") ?? "owner";
        var repo = request.RepositoryName ?? config.GetValueOrDefault("repository") ?? config.GetValueOrDefault("repo") ?? "repo";
        var defaultBranch = config.GetValueOrDefault("defaultBranch") ?? "main";

        if (token is not null && owner == "owner")
        {
            var user = await _github.GetAuthenticatedUserAsync(token, ct);
            if (user is not null) owner = user.Login;
        }

        return (owner, repo, token, defaultBranch);
    }

    private static void ValidateMigrationOutput(string content)
    {
        JsonElement parsed;
        try { parsed = JsonSerializer.Deserialize<JsonElement>(content); }
        catch { throw new InvalidOperationException("AI migration output was not valid JSON"); }

        if (parsed.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("AI migration output must be a JSON object");

        foreach (var field in new[] { "summary", "upSql", "downSql" })
        {
            if (!parsed.TryGetProperty(field, out var el) || el.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(el.GetString()))
                throw new InvalidOperationException($"AI migration output missing required field: {field}");
        }
    }
}
