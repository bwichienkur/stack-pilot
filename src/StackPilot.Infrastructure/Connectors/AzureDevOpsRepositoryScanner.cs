using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class AzureDevOpsRepositoryScanner : IRepositoryScanner
{
    public AzureDevOpsRepositoryScanner(IHttpClientFactory _) { }

    public async Task<RepositoryScanResult> ScanAsync(ConnectorContext context, string repositoryName, CancellationToken ct = default)
    {
        var pat = context.Credentials.GetValueOrDefault("pat");
        var config = ParseConfig(context.ConfigJson);
        var organization = config.GetValueOrDefault("organization");
        var project = config.GetValueOrDefault("project");

        if (pat is null || string.IsNullOrWhiteSpace(organization))
            return Fallback(repositoryName, "Azure DevOps PAT and organization are required");

        try
        {
            var client = CreateClient(pat);
            var projectSegment = string.IsNullOrWhiteSpace(project) ? "" : $"{project}/";
            var url = $"https://dev.azure.com/{organization}/{projectSegment}_apis/git/repositories/{Uri.EscapeDataString(repositoryName)}?api-version=7.0";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return Fallback(repositoryName, $"Repository not found ({(int)response.StatusCode})");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var defaultBranch = json.TryGetProperty("defaultBranch", out var branch) ? branch.GetString()?.Replace("refs/heads/", "") : "main";
            var (stack, frameworks) = await DetectStackAsync(client, organization, projectSegment, repositoryName, defaultBranch ?? "main", ct);

            return new RepositoryScanResult
            {
                RepositoryName = repositoryName,
                ApplicationName = repositoryName,
                Purpose = "Azure DevOps repository scan",
                TechnologyStack = stack,
                Frameworks = frameworks,
                Metadata = new()
                {
                    ["provider"] = "azure_devops",
                    ["organization"] = organization,
                    ["project"] = project ?? "",
                    ["defaultBranch"] = defaultBranch ?? "main"
                }
            };
        }
        catch (Exception ex)
        {
            return Fallback(repositoryName, ex.Message);
        }
    }

    private static async Task<(List<string> Stack, List<string> Frameworks)> DetectStackAsync(
        HttpClient client, string organization, string projectSegment, string repositoryName, string branch, CancellationToken ct)
    {
        var stack = new List<string>();
        var frameworks = new List<string>();

        foreach (var path in new[] { "/package.json", "/StackPilot.sln", "/pom.xml", "/go.mod" })
        {
            var itemUrl = $"https://dev.azure.com/{organization}/{projectSegment}_apis/git/repositories/{Uri.EscapeDataString(repositoryName)}/items?path={Uri.EscapeDataString(path)}&versionDescriptor.version={Uri.EscapeDataString(branch)}&api-version=7.0";
            var itemRes = await client.GetAsync(itemUrl, ct);
            if (!itemRes.IsSuccessStatusCode) continue;

            var content = await itemRes.Content.ReadAsStringAsync(ct);
            if (path.EndsWith("package.json", StringComparison.Ordinal))
            {
                stack.Add("Node.js");
                if (content.Contains("\"next\"", StringComparison.Ordinal)) frameworks.Add("Next.js");
                if (content.Contains("\"react\"", StringComparison.Ordinal)) frameworks.Add("React");
            }
            else if (path.EndsWith(".sln", StringComparison.Ordinal)) stack.Add(".NET");
            else if (path.EndsWith("pom.xml", StringComparison.Ordinal)) stack.Add("Java");
            else if (path.EndsWith("go.mod", StringComparison.Ordinal)) stack.Add("Go");
            break;
        }

        if (stack.Count == 0) stack.Add("Unknown");
        return (stack, frameworks);
    }

    private static HttpClient CreateClient(string pat)
    {
        var client = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static Dictionary<string, string> ParseConfig(string configJson)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(configJson) ?? new(); }
        catch { return new(); }
    }

    private static RepositoryScanResult Fallback(string repo, string reason) => new()
    {
        RepositoryName = repo,
        ApplicationName = repo,
        Purpose = reason,
        TechnologyStack = ["Unknown"],
        Metadata = new() { ["scanNote"] = reason, ["provider"] = "azure_devops" }
    };
}
