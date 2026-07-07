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

            return new RepositoryScanResult
            {
                RepositoryName = repositoryName,
                ApplicationName = repositoryName,
                Purpose = "Azure DevOps repository inventory scan",
                TechnologyStack = ["Unknown"],
                Frameworks = [],
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
