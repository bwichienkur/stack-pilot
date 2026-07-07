using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class JiraConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public JiraConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "jira";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.None;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (baseUrl, email, apiToken) = GetCredentials(context);
        if (baseUrl is null || email is null || apiToken is null)
            return new ConnectionTestResult { Success = false, Message = "Jira base URL, email, and API token are required" };

        try
        {
            var client = CreateClient(baseUrl, email, apiToken);
            var response = await client.GetAsync("/rest/api/3/myself", ct);
            if (!response.IsSuccessStatusCode)
                return new ConnectionTestResult { Success = false, Message = $"Jira API returned {(int)response.StatusCode}" };

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var displayName = json.TryGetProperty("displayName", out var name) ? name.GetString() : email;
            return new ConnectionTestResult { Success = true, Message = $"Connected to Jira as {displayName}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = $"Jira connection failed: {ex.Message}" };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (baseUrl, email, apiToken) = GetCredentials(context);
        if (baseUrl is null || email is null || apiToken is null)
            return new SyncResult { Success = false, Errors = ["Missing Jira credentials"] };

        var config = ParseConfig(context.ConfigJson);
        var projectKeys = config.GetValueOrDefault("projects", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            var client = CreateClient(baseUrl, email, apiToken);
            var projects = new List<string>();

            if (projectKeys.Length > 0)
            {
                projects.AddRange(projectKeys);
            }
            else
            {
                var response = await client.GetAsync("/rest/api/3/project/search?maxResults=50", ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                    if (json.TryGetProperty("values", out var values))
                    {
                        foreach (var project in values.EnumerateArray())
                        {
                            if (project.TryGetProperty("key", out var key))
                                projects.Add(key.GetString() ?? "");
                        }
                    }
                }
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = projects.Count,
                Metadata = new() { ["projects"] = projects.ToArray(), ["syncType"] = "jira" }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private HttpClient CreateClient(string baseUrl, string email, string apiToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static (string? BaseUrl, string? Email, string? ApiToken) GetCredentials(ConnectorContext context)
    {
        var config = ParseConfig(context.ConfigJson);
        var baseUrl = config.GetValueOrDefault("baseUrl") ?? context.Credentials.GetValueOrDefault("base_url");
        var email = context.Credentials.GetValueOrDefault("email");
        var apiToken = context.Credentials.GetValueOrDefault("api_token");
        return (baseUrl, email, apiToken);
    }
}
