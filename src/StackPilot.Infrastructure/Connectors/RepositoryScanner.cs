using System.Text.Json;
using System.Text.RegularExpressions;
using StackPilot.Application.Connectors;
using StackPilot.Infrastructure.External;

namespace StackPilot.Infrastructure.Connectors;

public partial class GitHubRepositoryScanner : IRepositoryScanner
{
    private readonly IGitHubApiClient _github;

    public GitHubRepositoryScanner(IGitHubApiClient github) => _github = github;

    public async Task<RepositoryScanResult> ScanAsync(ConnectorContext context, string repositoryName, CancellationToken ct = default)
    {
        var token = context.Credentials.GetValueOrDefault("pat")
            ?? context.Credentials.GetValueOrDefault("oauth_token");

        if (token is null)
            return CreateFallbackResult(repositoryName, "No GitHub token available");

        var config = ParseConfig(context.ConfigJson);
        var owner = config.GetValueOrDefault("owner", "");
        if (string.IsNullOrEmpty(owner))
        {
            var user = await _github.GetAuthenticatedUserAsync(token, ct);
            owner = user?.Login ?? "";
        }

        if (string.IsNullOrEmpty(owner))
            return CreateFallbackResult(repositoryName, "Could not determine repository owner");

        var repo = await _github.GetRepositoryAsync(token, owner, repositoryName, ct);
        if (repo is null)
            return CreateFallbackResult(repositoryName, $"Repository {owner}/{repositoryName} not found");

        var languages = await _github.GetLanguagesAsync(token, owner, repositoryName, ct);
        var rootContents = await _github.GetContentsAsync(token, owner, repositoryName, "", ct);

        var result = new RepositoryScanResult
        {
            RepositoryName = repositoryName,
            ApplicationName = repo.Name,
            Purpose = repo.Description ?? $"Repository: {repo.FullName}",
            TechnologyStack = languages.Keys.ToList(),
            Frameworks = DetectFrameworks(rootContents, languages.Keys),
            EntryPoints = DetectEntryPoints(rootContents),
            Dependencies = await DetectDependencies(token, owner, repositoryName, rootContents, ct),
            TestProjects = rootContents.Where(c => c.Name.Contains("test", StringComparison.OrdinalIgnoreCase) || c.Name.EndsWith(".Tests")).Select(c => c.Name).ToList(),
            SecurityRisks = [],
            RefactorOpportunities = []
        };

        var csprojContent = await FindAndReadFile(token, owner, repositoryName, rootContents, ".csproj", ct);
        if (csprojContent is not null)
        {
            result.ApiEndpoints.AddRange(ExtractApiRoutes(csprojContent));
            result.SecurityRisks.AddRange(DetectOutdatedPackages(csprojContent));
        }

        var packageJson = await FindAndReadFile(token, owner, repositoryName, rootContents, "package.json", ct);
        if (packageJson is not null)
        {
            result.Frameworks.AddRange(DetectNodeFrameworks(packageJson));
            result.Dependencies.AddRange(ParsePackageJsonDependencies(packageJson));
        }

        if (result.Dependencies.Any(d => d.IsOutdated))
            result.SecurityRisks.Add("Outdated dependencies detected");

        if (!result.TestProjects.Any())
            result.RefactorOpportunities.Add("No test projects detected — consider adding unit tests");

        result.Metadata["defaultBranch"] = repo.DefaultBranch ?? "main";
        result.Metadata["language"] = repo.Language ?? languages.Keys.FirstOrDefault() ?? "unknown";
        result.Metadata["pushedAt"] = repo.PushedAt?.ToString("O") ?? "";

        return result;
    }

    private async Task<string?> FindAndReadFile(string token, string owner, string repo, List<GitHubContent> root, string fileName, CancellationToken ct)
    {
        var match = root.FirstOrDefault(c => c.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
            || c.Name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return await _github.GetFileContentAsync(token, owner, repo, match.Path, ct);

        foreach (var dir in root.Where(c => c.Type == "dir").Take(5))
        {
            var contents = await _github.GetContentsAsync(token, owner, repo, dir.Path, ct);
            match = contents.FirstOrDefault(c => c.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                || c.Name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return await _github.GetFileContentAsync(token, owner, repo, match.Path, ct);
        }
        return null;
    }

    private async Task<List<DependencyInfo>> DetectDependencies(string token, string owner, string repo, List<GitHubContent> root, CancellationToken ct)
    {
        var deps = new List<DependencyInfo>();
        var csprojFiles = root.Where(c => c.Name.EndsWith(".csproj")).ToList();

        foreach (var dir in root.Where(c => c.Type == "dir").Take(3))
        {
            var contents = await _github.GetContentsAsync(token, owner, repo, dir.Path, ct);
            csprojFiles.AddRange(contents.Where(c => c.Name.EndsWith(".csproj")));
        }

        foreach (var csproj in csprojFiles.Take(5))
        {
            var content = await _github.GetFileContentAsync(token, owner, repo, csproj.Path, ct);
            if (content is not null)
                deps.AddRange(ParseCsprojDependencies(content));
        }
        return deps.DistinctBy(d => d.Name).ToList();
    }

    private static List<DependencyInfo> ParseCsprojDependencies(string content)
    {
        var deps = new List<DependencyInfo>();
        var matches = CsprojPackageRegex().Matches(content);
        foreach (Match m in matches)
        {
            deps.Add(new DependencyInfo
            {
                Name = m.Groups[1].Value,
                Version = m.Groups[2].Value,
                IsOutdated = IsLikelyOutdated(m.Groups[1].Value, m.Groups[2].Value)
            });
        }
        return deps;
    }

    private static List<DependencyInfo> ParsePackageJsonDependencies(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var deps = new List<DependencyInfo>();
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (doc.RootElement.TryGetProperty(section, out var obj))
                {
                    foreach (var prop in obj.EnumerateObject())
                    {
                        deps.Add(new DependencyInfo { Name = prop.Name, Version = prop.Value.GetString() ?? "", IsOutdated = prop.Value.GetString()?.StartsWith("^0.") == true });
                    }
                }
            }
            return deps;
        }
        catch { return []; }
    }

    private static List<string> DetectFrameworks(List<GitHubContent> root, IEnumerable<string> languages)
    {
        var frameworks = new List<string>();
        if (languages.Any(l => l is "C#" or "HTML")) frameworks.Add("ASP.NET Core");
        if (root.Any(c => c.Name == "next.config.js" || c.Name == "next.config.mjs")) frameworks.Add("Next.js");
        if (root.Any(c => c.Name == "docker-compose.yml")) frameworks.Add("Docker");
        if (root.Any(c => c.Name.Contains(".sln"))) frameworks.Add(".NET Solution");
        return frameworks;
    }

    private static List<string> DetectEntryPoints(List<GitHubContent> root) =>
        root.Where(c => c.Name is "Program.cs" or "Startup.cs" or "main.ts" or "index.ts" or "app.py")
            .Select(c => c.Path).ToList();

    private static List<string> DetectNodeFrameworks(string packageJson)
    {
        var frameworks = new List<string>();
        if (packageJson.Contains("\"next\"")) frameworks.Add("Next.js");
        if (packageJson.Contains("\"react\"")) frameworks.Add("React");
        if (packageJson.Contains("\"@angular/core\"")) frameworks.Add("Angular");
        if (packageJson.Contains("\"vue\"")) frameworks.Add("Vue");
        return frameworks;
    }

    private static List<string> ExtractApiRoutes(string csproj) => [];

    private static List<string> DetectOutdatedPackages(string csproj)
    {
        var risks = new List<string>();
        if (csproj.Contains("Newtonsoft.Json") && !csproj.Contains("13."))
            risks.Add("Outdated Newtonsoft.Json package");
        return risks;
    }

    private static bool IsLikelyOutdated(string name, string version)
    {
        if (name == "Newtonsoft.Json" && !version.StartsWith("13.")) return true;
        return version.StartsWith("1.") || version.StartsWith("2.") || version.StartsWith("3.");
    }

    private static Dictionary<string, string> ParseConfig(string configJson)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new(); }
        catch { return new(); }
    }

    private static RepositoryScanResult CreateFallbackResult(string repo, string reason) => new()
    {
        RepositoryName = repo,
        ApplicationName = repo,
        Purpose = reason,
        TechnologyStack = ["Unknown"],
        Metadata = new() { ["scanNote"] = reason }
    };

    [GeneratedRegex(@"PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""")]
    private static partial Regex CsprojPackageRegex();
}
