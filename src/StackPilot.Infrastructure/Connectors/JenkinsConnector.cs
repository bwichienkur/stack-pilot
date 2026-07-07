using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class JenkinsConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public JenkinsConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "jenkins";
    public override ConnectorCapabilities Capabilities =>
        ConnectorCapabilities.CiCdTracking | ConnectorCapabilities.DeploymentTracking;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var baseUrl = config.GetValueOrDefault("baseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ConnectionTestResult { Success = false, Message = "Jenkins base URL is required" };

        try
        {
            var client = CreateClient(context, baseUrl);
            var response = await client.GetAsync("/api/json?tree=jobs[name]", ct);
            return response.IsSuccessStatusCode
                ? new ConnectionTestResult { Success = true, Message = $"Connected to Jenkins at {baseUrl}" }
                : new ConnectionTestResult { Success = false, Message = $"Jenkins API returned {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = ex.Message };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var config = ParseConfig(context.ConfigJson);
        var baseUrl = config.GetValueOrDefault("baseUrl");
        var jobsConfig = config.GetValueOrDefault("jobs", "all");

        if (string.IsNullOrWhiteSpace(baseUrl))
            return new SyncResult { Success = false, Errors = ["Jenkins base URL is required"] };

        try
        {
            var client = CreateClient(context, baseUrl);
            var response = await client.GetAsync("/api/json?tree=jobs[name,url,color,lastBuild[number,result,timestamp]]", ct);
            if (!response.IsSuccessStatusCode)
                return new SyncResult { Success = false, Errors = [$"Jenkins API returned {(int)response.StatusCode}"] };

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var builds = new List<object>();
            var count = 0;

            if (json.TryGetProperty("jobs", out var jobs))
            {
                foreach (var job in jobs.EnumerateArray())
                {
                    var name = job.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (!jobsConfig.Equals("all", StringComparison.OrdinalIgnoreCase))
                    {
                        var filter = jobsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (!filter.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    }

                    count++;
                    builds.Add(new
                    {
                        job = name,
                        color = job.TryGetProperty("color", out var c) ? c.GetString() : null,
                        lastBuild = job.TryGetProperty("lastBuild", out var lb) ? new
                        {
                            number = lb.TryGetProperty("number", out var num) ? num.GetInt32() : 0,
                            result = lb.TryGetProperty("result", out var res) ? res.GetString() : null
                        } : null
                    });
                }
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = count,
                Metadata = new()
                {
                    ["jobs"] = builds,
                    ["syncType"] = "cicd"
                }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private static HttpClient CreateClient(ConnectorContext context, string baseUrl)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        var username = context.Credentials.GetValueOrDefault("username");
        var apiToken = context.Credentials.GetValueOrDefault("api_token") ?? context.Credentials.GetValueOrDefault("password");
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(apiToken))
        {
            var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
