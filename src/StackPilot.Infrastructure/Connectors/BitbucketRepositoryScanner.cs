using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using StackPilot.Application.Connectors;

namespace StackPilot.Infrastructure.Connectors;

public class BitbucketRepositoryScanner : IRepositoryScanner
{
    public BitbucketRepositoryScanner(IHttpClientFactory _) { }

    public async Task<RepositoryScanResult> ScanAsync(ConnectorContext context, string repositoryName, CancellationToken ct = default)
    {
        var username = context.Credentials.GetValueOrDefault("username");
        var appPassword = context.Credentials.GetValueOrDefault("app_password");
        var config = ParseConfig(context.ConfigJson);
        var workspace = config.GetValueOrDefault("workspace");

        if (username is null || appPassword is null || string.IsNullOrWhiteSpace(workspace))
            return Fallback(repositoryName, "Bitbucket credentials and workspace are required");

        try
        {
            var client = CreateClient(username, appPassword);
            var url = $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repositoryName}";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return Fallback(repositoryName, $"Repository not found ({(int)response.StatusCode})");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var language = json.TryGetProperty("language", out var lang) ? lang.GetString() : "unknown";
            var description = json.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var mainbranch = json.TryGetProperty("mainbranch", out var mb) && mb.TryGetProperty("name", out var mbName)
                ? mbName.GetString() ?? "main" : "main";

            var frameworks = new List<string>();
            var stack = language is not null ? new List<string> { language } : new List<string>();
            var pkgRes = await client.GetAsync(
                $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repositoryName}/src/{mainbranch}/package.json", ct);
            if (pkgRes.IsSuccessStatusCode)
            {
                var pkg = await pkgRes.Content.ReadAsStringAsync(ct);
                if (!stack.Contains("JavaScript")) stack.Add("JavaScript");
                if (pkg.Contains("\"next\"", StringComparison.Ordinal)) frameworks.Add("Next.js");
                if (pkg.Contains("\"react\"", StringComparison.Ordinal)) frameworks.Add("React");
            }

            return new RepositoryScanResult
            {
                RepositoryName = repositoryName,
                ApplicationName = repositoryName,
                Purpose = description ?? "Bitbucket repository scan",
                TechnologyStack = stack.Count > 0 ? stack : ["Unknown"],
                Frameworks = frameworks,
                Metadata = new()
                {
                    ["provider"] = "bitbucket",
                    ["workspace"] = workspace,
                    ["language"] = language ?? "unknown"
                }
            };
        }
        catch (Exception ex)
        {
            return Fallback(repositoryName, ex.Message);
        }
    }

    private static HttpClient CreateClient(string username, string appPassword)
    {
        var client = new HttpClient();
        var auth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{appPassword}"));
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
        Metadata = new() { ["scanNote"] = reason, ["provider"] = "bitbucket" }
    };
}
