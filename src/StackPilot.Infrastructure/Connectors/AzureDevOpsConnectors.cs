using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class AzureDevOpsRepositoryConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AzureDevOpsRepositoryConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "azure_devops_repository";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.RepositoryScan | ConnectorCapabilities.CodeIndexing;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var pat = GetPat(context);
        var config = ParseConfig(context.ConfigJson);
        var organization = config.GetValueOrDefault("organization");
        if (pat is null || string.IsNullOrWhiteSpace(organization))
            return new ConnectionTestResult { Success = false, Message = "Azure DevOps PAT and organization are required" };

        try
        {
            var client = CreateClient(pat);
            var response = await client.GetAsync($"https://dev.azure.com/{organization}/_apis/projects?api-version=7.0&$top=1", ct);
            return response.IsSuccessStatusCode
                ? new ConnectionTestResult { Success = true, Message = $"Connected to Azure DevOps org {organization}" }
                : new ConnectionTestResult { Success = false, Message = $"Azure DevOps API returned {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = ex.Message };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var pat = GetPat(context);
        var config = ParseConfig(context.ConfigJson);
        var organization = config.GetValueOrDefault("organization");
        var project = config.GetValueOrDefault("project");
        var reposConfig = config.GetValueOrDefault("repositories", "all");

        if (pat is null || string.IsNullOrWhiteSpace(organization))
            return new SyncResult { Success = false, Errors = ["Azure DevOps PAT and organization are required"] };

        try
        {
            var client = CreateClient(pat);
            var repoNames = new List<string>();

            if (!string.IsNullOrWhiteSpace(project))
            {
                repoNames.AddRange(await ListReposForProjectAsync(client, organization, project, reposConfig, ct));
            }
            else
            {
                var projectsResponse = await client.GetAsync($"https://dev.azure.com/{organization}/_apis/projects?api-version=7.0", ct);
                if (projectsResponse.IsSuccessStatusCode)
                {
                    var projectsJson = await projectsResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                    if (projectsJson.TryGetProperty("value", out var projects))
                    {
                        foreach (var proj in projects.EnumerateArray())
                        {
                            if (!proj.TryGetProperty("name", out var nameProp)) continue;
                            var projName = nameProp.GetString() ?? "";
                            repoNames.AddRange(await ListReposForProjectAsync(client, organization, projName, reposConfig, ct));
                        }
                    }
                }
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = repoNames.Count,
                Metadata = new()
                {
                    ["repositories"] = repoNames.Distinct().ToArray(),
                    ["organization"] = organization,
                    ["syncType"] = "repository"
                }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private static async Task<List<string>> ListReposForProjectAsync(HttpClient client, string organization, string project, string reposConfig, CancellationToken ct)
    {
        var response = await client.GetAsync(
            $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_apis/git/repositories?api-version=7.0", ct);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!json.TryGetProperty("value", out var repos)) return [];

        var allNames = repos.EnumerateArray()
            .Select(r => r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (reposConfig.Equals("all", StringComparison.OrdinalIgnoreCase))
            return allNames;

        var filter = reposConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return allNames.Where(n => filter.Contains(n)).ToList();
    }

    private static string? GetPat(ConnectorContext context) =>
        context.Credentials.GetValueOrDefault("pat") ?? context.Credentials.GetValueOrDefault("api_token");

    private static HttpClient CreateClient(string pat)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}")));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}

public class AzurePipelinesConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AzurePipelinesConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "azure_pipelines";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.CiCdTracking | ConnectorCapabilities.DeploymentTracking;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var pat = GetPat(context);
        var config = ParseConfig(context.ConfigJson);
        var organization = config.GetValueOrDefault("organization");
        if (pat is null || string.IsNullOrWhiteSpace(organization))
            return new ConnectionTestResult { Success = false, Message = "Azure DevOps PAT and organization are required" };

        try
        {
            var client = CreateClient(pat);
            var response = await client.GetAsync($"https://dev.azure.com/{organization}/_apis/pipelines?api-version=7.0&$top=1", ct);
            return response.IsSuccessStatusCode
                ? new ConnectionTestResult { Success = true, Message = $"Azure Pipelines ready for {organization}" }
                : new ConnectionTestResult { Success = false, Message = $"Azure Pipelines API returned {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = ex.Message };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var pat = GetPat(context);
        var config = ParseConfig(context.ConfigJson);
        var organization = config.GetValueOrDefault("organization");
        var project = config.GetValueOrDefault("project");

        if (pat is null || string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
            return new SyncResult { Success = false, Errors = ["Azure DevOps PAT, organization, and project are required"] };

        try
        {
            var client = CreateClient(pat);
            var response = await client.GetAsync(
                $"https://dev.azure.com/{organization}/{Uri.EscapeDataString(project)}/_apis/build/builds?api-version=7.0&$top=25&queryOrder=finishTimeDescending",
                ct);

            if (!response.IsSuccessStatusCode)
                return new SyncResult { Success = false, Errors = [$"Azure Pipelines API returned {(int)response.StatusCode}"] };

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var builds = new List<object>();
            var count = 0;

            if (json.TryGetProperty("value", out var values))
            {
                foreach (var build in values.EnumerateArray())
                {
                    count++;
                    builds.Add(new
                    {
                        id = build.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        status = build.TryGetProperty("status", out var st) ? st.GetString() : null,
                        result = build.TryGetProperty("result", out var res) ? res.GetString() : null,
                        buildNumber = build.TryGetProperty("buildNumber", out var bn) ? bn.GetString() : null
                    });
                }
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = count,
                Metadata = new()
                {
                    ["builds"] = builds,
                    ["syncType"] = "cicd"
                }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private static string? GetPat(ConnectorContext context) =>
        context.Credentials.GetValueOrDefault("pat") ?? context.Credentials.GetValueOrDefault("api_token");

    private static HttpClient CreateClient(string pat)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}")));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
