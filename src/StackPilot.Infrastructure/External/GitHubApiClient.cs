using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StackPilot.Infrastructure.External;

public interface IGitHubApiClient
{
    Task<GitHubUser?> GetAuthenticatedUserAsync(string token, CancellationToken ct = default);
    Task<List<GitHubRepo>> ListRepositoriesAsync(string token, string owner, CancellationToken ct = default);
    Task<GitHubRepo?> GetRepositoryAsync(string token, string owner, string repo, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetLanguagesAsync(string token, string owner, string repo, CancellationToken ct = default);
    Task<List<GitHubContent>> GetContentsAsync(string token, string owner, string repo, string path = "", CancellationToken ct = default);
    Task<string?> GetFileContentAsync(string token, string owner, string repo, string path, CancellationToken ct = default);
    Task<List<GitHubWorkflowRun>> ListWorkflowRunsAsync(string token, string owner, string repo, int perPage = 10, CancellationToken ct = default);
}

public class GitHubApiClient : IGitHubApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitHubApiClient(HttpClient http) => _http = http;

    public async Task<GitHubUser?> GetAuthenticatedUserAsync(string token, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "https://api.github.com/user", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitHubUser>(JsonOptions, ct);
    }

    public async Task<List<GitHubRepo>> ListRepositoriesAsync(string token, string owner, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/orgs/{owner}/repos?per_page=100";
        using var request = CreateRequest(HttpMethod.Get, url, token);
        var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            url = $"https://api.github.com/users/{owner}/repos?per_page=100";
            using var userRequest = CreateRequest(HttpMethod.Get, url, token);
            response = await _http.SendAsync(userRequest, ct);
        }
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<GitHubRepo>>(JsonOptions, ct) ?? [];
    }

    public async Task<GitHubRepo?> GetRepositoryAsync(string token, string owner, string repo, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitHubRepo>(JsonOptions, ct);
    }

    public async Task<Dictionary<string, int>> GetLanguagesAsync(string token, string owner, string repo, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/languages", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return new();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(JsonOptions, ct) ?? new();
    }

    public async Task<List<GitHubContent>> GetContentsAsync(string token, string owner, string repo, string path = "", CancellationToken ct = default)
    {
        var url = string.IsNullOrEmpty(path)
            ? $"https://api.github.com/repos/{owner}/{repo}/contents"
            : $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";
        using var request = CreateRequest(HttpMethod.Get, url, token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        var content = await response.Content.ReadAsStringAsync(ct);
        if (content.TrimStart().StartsWith('['))
            return JsonSerializer.Deserialize<List<GitHubContent>>(content, JsonOptions) ?? [];
        var single = JsonSerializer.Deserialize<GitHubContent>(content, JsonOptions);
        return single is null ? [] : [single];
    }

    public async Task<string?> GetFileContentAsync(string token, string owner, string repo, string path, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/contents/{path}", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<List<GitHubWorkflowRun>> ListWorkflowRunsAsync(string token, string owner, string repo, int perPage = 10, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/actions/runs?per_page={perPage}", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        var result = await response.Content.ReadFromJsonAsync<GitHubWorkflowRunsResponse>(JsonOptions, ct);
        return result?.WorkflowRuns ?? [];
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("StackPilot/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return request;
    }
}

public class GitHubUser
{
    public string Login { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
}

public class GitHubRepo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Language { get; set; }
    public bool Private { get; set; }
    public DateTime? PushedAt { get; set; }
}

public class GitHubContent
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public class GitHubWorkflowRunsResponse
{
    [JsonPropertyName("workflow_runs")]
    public List<GitHubWorkflowRun> WorkflowRuns { get; set; } = [];
}

public class GitHubWorkflowRun
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
    public string? Conclusion { get; set; }
    public string? HtmlUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}
