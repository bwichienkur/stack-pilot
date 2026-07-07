using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class BitbucketRepositoryConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BitbucketRepositoryConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "bitbucket_repository";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.RepositoryScan | ConnectorCapabilities.CodeIndexing;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (username, appPassword) = GetCredentials(context);
        var config = ParseConfig(context.ConfigJson);
        var workspace = config.GetValueOrDefault("workspace");
        if (username is null || appPassword is null || string.IsNullOrWhiteSpace(workspace))
            return new ConnectionTestResult { Success = false, Message = "Bitbucket username, app password, and workspace are required" };

        try
        {
            var client = CreateClient(username, appPassword);
            var response = await client.GetAsync($"https://api.bitbucket.org/2.0/repositories/{workspace}?pagelen=1", ct);
            return response.IsSuccessStatusCode
                ? new ConnectionTestResult { Success = true, Message = $"Connected to Bitbucket workspace {workspace}" }
                : new ConnectionTestResult { Success = false, Message = $"Bitbucket API returned {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = ex.Message };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (username, appPassword) = GetCredentials(context);
        var config = ParseConfig(context.ConfigJson);
        var workspace = config.GetValueOrDefault("workspace");
        var reposConfig = config.GetValueOrDefault("repositories", "all");

        if (username is null || appPassword is null || string.IsNullOrWhiteSpace(workspace))
            return new SyncResult { Success = false, Errors = ["Bitbucket credentials and workspace are required"] };

        try
        {
            var client = CreateClient(username, appPassword);
            var response = await client.GetAsync($"https://api.bitbucket.org/2.0/repositories/{workspace}?pagelen=100", ct);
            if (!response.IsSuccessStatusCode)
                return new SyncResult { Success = false, Errors = [$"Bitbucket API returned {(int)response.StatusCode}"] };

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var repoNames = new List<string>();

            if (json.TryGetProperty("values", out var values))
            {
                foreach (var repo in values.EnumerateArray())
                {
                    if (repo.TryGetProperty("slug", out var slug))
                        repoNames.Add(slug.GetString() ?? "");
                }
            }

            if (!reposConfig.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var filter = reposConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                repoNames = repoNames.Where(r => filter.Contains(r)).ToList();
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = repoNames.Count,
                Metadata = new()
                {
                    ["repositories"] = repoNames.ToArray(),
                    ["workspace"] = workspace,
                    ["syncType"] = "repository"
                }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private static (string? Username, string? AppPassword) GetCredentials(ConnectorContext context)
    {
        var username = context.Credentials.GetValueOrDefault("username");
        var appPassword = context.Credentials.GetValueOrDefault("app_password") ?? context.Credentials.GetValueOrDefault("api_token");
        return (username, appPassword);
    }

    private static HttpClient CreateClient(string username, string appPassword)
    {
        var client = new HttpClient();
        var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{appPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
