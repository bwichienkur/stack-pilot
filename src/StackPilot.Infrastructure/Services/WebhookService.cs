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

        var connector = await _db.ConnectorInstances
            .IgnoreQueryFilters()
            .Include(ci => ci.Definition)
            .Where(ci => ci.Definition.Type == "github_actions" && ci.ConfigJson.Contains(repoFullName))
            .FirstOrDefaultAsync(ct);

        if (connector is null) return;

        var buildRun = await _db.BuildRuns
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.ExternalId == externalId, ct);

        if (buildRun is null)
        {
            buildRun = new BuildRun
            {
                OrganizationId = connector.OrganizationId,
                ConnectorId = connector.Id,
                ExternalId = externalId
            };
            _db.BuildRuns.Add(buildRun);
        }

        buildRun.Status = MapStatus(status, conclusion);
        buildRun.Conclusion = conclusion;
        buildRun.LogsUrl = htmlUrl;
        buildRun.StartedAt ??= run.TryGetProperty("run_started_at", out var started) && started.ValueKind == JsonValueKind.String
            ? DateTime.Parse(started.GetString()!) : DateTime.UtcNow;

        if (status is "completed")
            buildRun.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
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
