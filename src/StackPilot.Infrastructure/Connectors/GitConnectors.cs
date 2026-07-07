using System.Text.Json;
using System.Text.RegularExpressions;
using StackPilot.Application.Connectors;
using StackPilot.Infrastructure.External;

namespace StackPilot.Infrastructure.Connectors;

public partial class GitHubRepositoryConnector : ConnectorBase
{
    private readonly IGitHubApiClient _github;

    public GitHubRepositoryConnector(IGitHubApiClient github) => _github = github;

    public override string Type => "github_repository";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.RepositoryScan | ConnectorCapabilities.CodeIndexing | ConnectorCapabilities.WebhookReceive;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new ConnectionTestResult { Success = false, Message = "GitHub token is required" };

        var user = await _github.GetAuthenticatedUserAsync(token, ct);
        if (user is null)
            return new ConnectionTestResult { Success = false, Message = "Failed to authenticate with GitHub API" };

        var config = ParseConfig(context.ConfigJson);
        var owner = config.GetValueOrDefault("owner", user.Login);

        return new ConnectionTestResult
        {
            Success = true,
            Message = $"Connected to GitHub as {user.Login}",
            Details = new() { ["owner"] = owner, ["login"] = user.Login }
        };
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new SyncResult { Success = false, Errors = ["GitHub token is required"] };

        var config = ParseConfig(context.ConfigJson);
        var user = await _github.GetAuthenticatedUserAsync(token, ct);
        var owner = config.GetValueOrDefault("owner", user?.Login ?? "");

        if (string.IsNullOrEmpty(owner))
            return new SyncResult { Success = false, Errors = ["GitHub owner is required"] };

        var reposConfig = config.GetValueOrDefault("repositories", "all");
        List<string> repoNames;

        if (reposConfig.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var repos = await _github.ListRepositoriesAsync(token, owner, ct);
            repoNames = repos.Select(r => r.Name).ToList();
        }
        else
        {
            repoNames = reposConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = repoNames.Count,
            Metadata = new() { ["repositories"] = repoNames.ToArray(), ["owner"] = owner, ["syncType"] = "repository" }
        };
    }

    private static string? GetToken(ConnectorContext context) =>
        context.Credentials.GetValueOrDefault("pat") ?? context.Credentials.GetValueOrDefault("oauth_token");
}

public partial class GitHubActionsConnector : ConnectorBase
{
    private readonly IGitHubApiClient _github;

    public GitHubActionsConnector(IGitHubApiClient github) => _github = github;

    public override string Type => "github_actions";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.CiCdTracking | ConnectorCapabilities.WebhookReceive | ConnectorCapabilities.DeploymentTracking;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new ConnectionTestResult { Success = false, Message = "GitHub token is required" };

        var user = await _github.GetAuthenticatedUserAsync(token, ct);
        return new ConnectionTestResult
        {
            Success = user is not null,
            Message = user is not null ? $"GitHub Actions ready for {user.Login}" : "Failed to authenticate"
        };
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new SyncResult { Success = false, Errors = ["GitHub token is required"] };

        var config = ParseConfig(context.ConfigJson);
        var user = await _github.GetAuthenticatedUserAsync(token, ct);
        var owner = config.GetValueOrDefault("owner", user?.Login ?? "");
        var repos = config.GetValueOrDefault("repositories", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var totalRuns = 0;
        var runSummaries = new List<object>();

        foreach (var repo in repos)
        {
            var runs = await _github.ListWorkflowRunsAsync(token, owner, repo, 5, ct);
            totalRuns += runs.Count;
            runSummaries.AddRange(runs.Select(r => new { repo, r.Id, r.Status, r.Conclusion, r.HtmlUrl }));
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = totalRuns,
            Metadata = new() { ["workflowRuns"] = totalRuns, ["runs"] = runSummaries, ["syncType"] = "cicd" }
        };
    }

    private static string? GetToken(ConnectorContext context) =>
        context.Credentials.GetValueOrDefault("pat") ?? context.Credentials.GetValueOrDefault("oauth_token");
}

public class GitLabRepositoryConnector : ConnectorBase
{
    private readonly IGitLabApiClient _gitlab;

    public GitLabRepositoryConnector(IGitLabApiClient gitlab) => _gitlab = gitlab;

    public override string Type => "gitlab_repository";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.RepositoryScan | ConnectorCapabilities.CodeIndexing | ConnectorCapabilities.CiCdTracking;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new ConnectionTestResult { Success = false, Message = "GitLab token is required" };

        var baseUrl = GetBaseUrl(context);
        var user = await _gitlab.GetAuthenticatedUserAsync(token, baseUrl, ct);
        return new ConnectionTestResult
        {
            Success = user is not null,
            Message = user is not null ? $"Connected to GitLab as {user.Username}" : "Failed to authenticate with GitLab"
        };
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var token = GetToken(context);
        if (token is null)
            return new SyncResult { Success = false, Errors = ["GitLab token is required"] };

        var config = ParseConfig(context.ConfigJson);
        var baseUrl = GetBaseUrl(context);
        var group = config.GetValueOrDefault("group");
        var projectsConfig = config.GetValueOrDefault("projects", "all");

        List<string> projectNames;
        if (projectsConfig.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var projects = await _gitlab.ListProjectsAsync(token, baseUrl, group, ct);
            projectNames = projects.Select(p => p.PathWithNamespace).ToList();
        }
        else
        {
            projectNames = projectsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return new SyncResult
        {
            Success = true,
            ItemsProcessed = projectNames.Count,
            Metadata = new() { ["projects"] = projectNames.ToArray(), ["baseUrl"] = baseUrl, ["syncType"] = "repository" }
        };
    }

    private static string? GetToken(ConnectorContext context) =>
        context.Credentials.GetValueOrDefault("pat") ?? context.Credentials.GetValueOrDefault("oauth_token");

    private static string GetBaseUrl(ConnectorContext context) =>
        ParseConfig(context.ConfigJson).GetValueOrDefault("baseUrl", "https://gitlab.com");
}
