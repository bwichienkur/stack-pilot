using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StackPilot.Infrastructure.External;

public interface IGitLabApiClient
{
    Task<GitLabUser?> GetAuthenticatedUserAsync(string token, string baseUrl, CancellationToken ct = default);
    Task<List<GitLabProject>> ListProjectsAsync(string token, string baseUrl, string? group = null, CancellationToken ct = default);
    Task<GitLabProject?> GetProjectAsync(string token, string baseUrl, string projectPath, CancellationToken ct = default);
    Task<List<GitLabPipeline>> ListPipelinesAsync(string token, string baseUrl, int projectId, int perPage = 10, CancellationToken ct = default);
}

public class GitLabApiClient : IGitLabApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GitLabApiClient(HttpClient http) => _http = http;

    public async Task<GitLabUser?> GetAuthenticatedUserAsync(string token, string baseUrl, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{NormalizeBaseUrl(baseUrl)}/api/v4/user", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitLabUser>(JsonOptions, ct);
    }

    public async Task<List<GitLabProject>> ListProjectsAsync(string token, string baseUrl, string? group = null, CancellationToken ct = default)
    {
        var url = group is not null
            ? $"{NormalizeBaseUrl(baseUrl)}/api/v4/groups/{Uri.EscapeDataString(group)}/projects?per_page=100"
            : $"{NormalizeBaseUrl(baseUrl)}/api/v4/projects?membership=true&per_page=100";
        using var request = CreateRequest(HttpMethod.Get, url, token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<GitLabProject>>(JsonOptions, ct) ?? [];
    }

    public async Task<GitLabProject?> GetProjectAsync(string token, string baseUrl, string projectPath, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(projectPath);
        using var request = CreateRequest(HttpMethod.Get, $"{NormalizeBaseUrl(baseUrl)}/api/v4/projects/{encoded}", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitLabProject>(JsonOptions, ct);
    }

    public async Task<List<GitLabPipeline>> ListPipelinesAsync(string token, string baseUrl, int projectId, int perPage = 10, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{NormalizeBaseUrl(baseUrl)}/api/v4/projects/{projectId}/pipelines?per_page={perPage}", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<GitLabPipeline>>(JsonOptions, ct) ?? [];
    }

    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.TrimEnd('/');

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        return request;
    }
}

public class GitLabUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class GitLabProject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PathWithNamespace { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DefaultBranch { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class GitLabPipeline
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Ref { get; set; }
    public string? WebUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
}
