using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly AppDbContext _db;

    public WebhookService(AppDbContext db) => _db = db;

    public async Task HandleGitHubEventAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        if (eventType != "workflow_run") return;

        using var doc = JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("workflow_run", out var run)) return;

        var externalId = run.GetProperty("id").GetInt64().ToString();
        var status = run.GetProperty("status").GetString() ?? "queued";
        var conclusion = run.TryGetProperty("conclusion", out var c) && c.ValueKind != JsonValueKind.Null
            ? c.GetString() : null;
        var htmlUrl = run.TryGetProperty("html_url", out var url) ? url.GetString() : null;
        var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString() ?? "";

        string? pullRequestUrl = null;
        if (run.TryGetProperty("pull_requests", out var prs) && prs.ValueKind == JsonValueKind.Array)
        {
            foreach (var pr in prs.EnumerateArray())
            {
                if (pr.TryGetProperty("url", out var prUrl))
                {
                    pullRequestUrl = prUrl.GetString()?.Replace("/api.github.com/repos", "https://github.com").Replace("/pulls/", "/pull/");
                    break;
                }
            }
        }

        var headBranch = run.TryGetProperty("head_branch", out var branch) ? branch.GetString() : null;

        var connector = await _db.ConnectorInstances
            .IgnoreQueryFilters()
            .Include(ci => ci.Definition)
            .Where(ci => ci.Definition.Type == "github_actions" && ci.ConfigJson.Contains(repoFullName))
            .FirstOrDefaultAsync(ct);

        if (connector is null) return;

        var ticketId = await ResolveTicketIdAsync(connector.WorkspaceId, headBranch, pullRequestUrl, ct);

        var buildRun = await _db.BuildRuns
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.ExternalId == externalId, ct);

        if (buildRun is null)
        {
            buildRun = new BuildRun
            {
                OrganizationId = connector.OrganizationId,
                ConnectorId = connector.Id,
                ExternalId = externalId,
                TicketId = ticketId
            };
            _db.BuildRuns.Add(buildRun);
        }
        else if (ticketId.HasValue && buildRun.TicketId is null)
        {
            buildRun.TicketId = ticketId;
        }

        buildRun.Status = MapStatus(status, conclusion);
        buildRun.Conclusion = conclusion;
        buildRun.LogsUrl = htmlUrl;
        if (!string.IsNullOrEmpty(pullRequestUrl))
            buildRun.PullRequestUrl = pullRequestUrl;

        buildRun.StartedAt ??= run.TryGetProperty("run_started_at", out var started) && started.ValueKind == JsonValueKind.String
            ? DateTime.Parse(started.GetString()!) : DateTime.UtcNow;

        if (status is "completed")
            buildRun.CompletedAt = DateTime.UtcNow;

        if (ticketId.HasValue)
        {
            var ticket = await _db.Tickets.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == ticketId.Value, ct);
            if (ticket is not null)
            {
                ticket.Status = buildRun.Status switch
                {
                    BuildStatus.InProgress => TicketStatus.BuildRunning,
                    BuildStatus.Completed => TicketStatus.DeployedToTest,
                    BuildStatus.Failed => TicketStatus.ImplementationInProgress,
                    _ => ticket.Status
                };
                if (!string.IsNullOrEmpty(pullRequestUrl))
                    ticket.Status = TicketStatus.PullRequestCreated;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Guid?> ResolveTicketIdAsync(Guid workspaceId, string? headBranch, string? pullRequestUrl, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(headBranch))
        {
            if (Guid.TryParse(headBranch.Replace("ticket/", "").Replace("feature/", ""), out var ticketGuid))
            {
                var byId = await _db.Tickets.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.WorkspaceId == workspaceId && t.Id == ticketGuid, ct);
                if (byId is not null) return byId.Id;
            }

            var numberPart = headBranch.Split('-', '/').LastOrDefault();
            if (int.TryParse(numberPart, out var ticketNumber))
            {
                var byNumber = await _db.Tickets.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t => t.WorkspaceId == workspaceId && t.TicketNumber == ticketNumber, ct);
                if (byNumber is not null) return byNumber.Id;
            }
        }

        if (!string.IsNullOrEmpty(pullRequestUrl))
        {
            var linked = await _db.BuildRuns.IgnoreQueryFilters()
                .Where(b => b.PullRequestUrl == pullRequestUrl && b.TicketId != null)
                .Select(b => b.TicketId)
                .FirstOrDefaultAsync(ct);
            if (linked.HasValue) return linked;
        }

        return null;
    }

    private static BuildStatus MapStatus(string status, string? conclusion) => status switch
    {
        "queued" => BuildStatus.Queued,
        "in_progress" => BuildStatus.InProgress,
        "completed" when conclusion is "failure" => BuildStatus.Failed,
        "completed" when conclusion is "cancelled" => BuildStatus.Cancelled,
        "completed" => BuildStatus.Completed,
        _ => BuildStatus.Queued
    };
}
