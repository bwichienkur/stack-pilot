using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class ServiceNowConnector : ConnectorBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceNowConnector(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public override string Type => "servicenow";
    public override ConnectorCapabilities Capabilities => ConnectorCapabilities.TicketSync;

    public override async Task<ConnectionTestResult> TestConnectionAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (instanceUrl, username, password) = GetCredentials(context);
        if (instanceUrl is null || username is null || password is null)
            return new ConnectionTestResult { Success = false, Message = "ServiceNow instance URL, username, and password are required" };

        try
        {
            var client = CreateClient(instanceUrl, username, password);
            var response = await client.GetAsync("/api/now/table/sys_user?sysparm_limit=1", ct);
            if (!response.IsSuccessStatusCode)
                return new ConnectionTestResult { Success = false, Message = $"ServiceNow API returned {(int)response.StatusCode}" };

            return new ConnectionTestResult { Success = true, Message = $"Connected to ServiceNow at {instanceUrl}" };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, Message = $"ServiceNow connection failed: {ex.Message}" };
        }
    }

    public override async Task<SyncResult> SyncAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var (instanceUrl, username, password) = GetCredentials(context);
        if (instanceUrl is null || username is null || password is null)
            return new SyncResult { Success = false, Errors = ["Missing ServiceNow credentials"] };

        var config = ParseConfig(context.ConfigJson);
        var table = config.GetValueOrDefault("table", "incident");
        var query = config.GetValueOrDefault("query", "active=true");

        try
        {
            var client = CreateClient(instanceUrl, username, password);
            var encodedQuery = Uri.EscapeDataString(query);
            var response = await client.GetAsync(
                $"/api/now/table/{table}?sysparm_query={encodedQuery}&sysparm_limit=50&sysparm_fields=number,short_description,description,state,priority,sys_class_name",
                ct);

            if (!response.IsSuccessStatusCode)
                return new SyncResult { Success = false, Errors = [$"ServiceNow API returned {(int)response.StatusCode}"] };

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var issues = new List<Dictionary<string, string>>();

            if (json.TryGetProperty("result", out var results))
            {
                foreach (var record in results.EnumerateArray())
                {
                    var number = record.TryGetProperty("number", out var num) ? num.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(number)) continue;

                    var title = record.TryGetProperty("short_description", out var sd) ? sd.GetString() ?? number : number;
                    var description = record.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
                    var state = record.TryGetProperty("state", out var st) ? st.GetString() ?? "Open" : "Open";
                    var priority = record.TryGetProperty("priority", out var pr) ? pr.GetString() ?? "3" : "3";
                    var recordType = record.TryGetProperty("sys_class_name", out var cls) ? cls.GetString() ?? table : table;

                    issues.Add(new Dictionary<string, string>
                    {
                        ["externalId"] = number,
                        ["title"] = title,
                        ["description"] = description,
                        ["status"] = MapState(state),
                        ["priority"] = MapPriority(priority),
                        ["ticketType"] = recordType
                    });
                }
            }

            return new SyncResult
            {
                Success = true,
                ItemsProcessed = issues.Count,
                Metadata = new()
                {
                    ["table"] = table,
                    ["issues"] = issues,
                    ["syncType"] = "servicenow"
                }
            };
        }
        catch (Exception ex)
        {
            return new SyncResult { Success = false, Errors = [ex.Message] };
        }
    }

    private static string MapState(string? state) => state switch
    {
        "1" or "New" => "New",
        "2" or "In Progress" => "In Progress",
        "6" or "Resolved" => "Resolved",
        "7" or "Closed" => "Closed",
        _ => state ?? "Open"
    };

    private static string MapPriority(string? priority) => priority switch
    {
        "1" => "Critical",
        "2" => "High",
        "4" => "Low",
        "5" => "Low",
        _ => "Medium"
    };

    private HttpClient CreateClient(string instanceUrl, string username, string password)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(instanceUrl.TrimEnd('/') + "/");
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static (string? InstanceUrl, string? Username, string? Password) GetCredentials(ConnectorContext context)
    {
        var config = ParseConfig(context.ConfigJson);
        var instanceUrl = config.GetValueOrDefault("instanceUrl") ?? context.Credentials.GetValueOrDefault("instance_url");
        var username = context.Credentials.GetValueOrDefault("username");
        var password = context.Credentials.GetValueOrDefault("password") ?? context.Credentials.GetValueOrDefault("api_token");
        return (instanceUrl, username, password);
    }
}
