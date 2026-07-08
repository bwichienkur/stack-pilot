using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
    Task<string?> GetBranchShaAsync(string token, string owner, string repo, string branch, CancellationToken ct = default);
    Task<GitHubRefResult?> CreateBranchAsync(string token, string owner, string repo, string branchName, string fromBranch, CancellationToken ct = default);
    Task<GitHubFileCommitResult?> CreateOrUpdateFileAsync(string token, string owner, string repo, string path, string branch, string content, string commitMessage, CancellationToken ct = default);
    Task<GitHubPullRequestResult?> CreatePullRequestAsync(string token, string owner, string repo, string title, string headBranch, string baseBranch, string? body = null, CancellationToken ct = default);
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

    public async Task<string?> GetBranchShaAsync(string token, string owner, string repo, string branch, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/git/ref/heads/{Uri.EscapeDataString(branch)}", token);
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<GitHubRefResponse>(JsonOptions, ct);
        return result?.Object?.Sha;
    }

    public async Task<GitHubRefResult?> CreateBranchAsync(string token, string owner, string repo, string branchName, string fromBranch, CancellationToken ct = default)
    {
        var sha = await GetBranchShaAsync(token, owner, repo, fromBranch, ct);
        if (sha is null) return null;

        using var request = CreateRequest(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repo}/git/refs", token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { @ref = $"refs/heads/{branchName}", sha }),
            Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitHubRefResult>(JsonOptions, ct);
    }

    public async Task<GitHubFileCommitResult?> CreateOrUpdateFileAsync(
        string token, string owner, string repo, string path, string branch, string content, string commitMessage, CancellationToken ct = default)
    {
        var encodedPath = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        var getUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{encodedPath}?ref={Uri.EscapeDataString(branch)}";
        using var getRequest = CreateRequest(HttpMethod.Get, getUrl, token);
        var getResponse = await _http.SendAsync(getRequest, ct);
        string? existingSha = null;
        if (getResponse.IsSuccessStatusCode)
        {
            var existing = await getResponse.Content.ReadFromJsonAsync<GitHubContentResponse>(JsonOptions, ct);
            existingSha = existing?.Sha;
        }

        var payload = new Dictionary<string, object?>
        {
            ["message"] = commitMessage,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"] = branch
        };
        if (existingSha is not null) payload["sha"] = existingSha;

        using var putRequest = CreateRequest(HttpMethod.Put, $"https://api.github.com/repos/{owner}/{repo}/contents/{encodedPath}", token);
        putRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var putResponse = await _http.SendAsync(putRequest, ct);
        if (!putResponse.IsSuccessStatusCode) return null;
        return await putResponse.Content.ReadFromJsonAsync<GitHubFileCommitResult>(JsonOptions, ct);
    }

    public async Task<GitHubPullRequestResult?> CreatePullRequestAsync(
        string token, string owner, string repo, string title, string headBranch, string baseBranch, string? body = null, CancellationToken ct = default)
    {
        var payload = new { title, head = headBranch, @base = baseBranch, body };
        using var request = CreateRequest(HttpMethod.Post, $"https://api.github.com/repos/{owner}/{repo}/pulls", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<GitHubPullRequestResult>(JsonOptions, ct);
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

public class GitHubRefResponse
{
    public GitHubRefObject? Object { get; set; }
}

public class GitHubRefObject
{
    public string? Sha { get; set; }
}

public class GitHubRefResult
{
    public string? Ref { get; set; }
    public GitHubRefObject? Object { get; set; }
}

public class GitHubContentResponse
{
    public string? Sha { get; set; }
}

public class GitHubFileCommitResult
{
    public GitHubFileCommitContent? Content { get; set; }
    public GitHubFileCommitInfo? Commit { get; set; }
}

public class GitHubFileCommitContent
{
    public string? Path { get; set; }
    public string? HtmlUrl { get; set; }
}

public class GitHubFileCommitInfo
{
    public string? Sha { get; set; }
}

public class GitHubPullRequestResult
{
    public long Number { get; set; }
    public string? HtmlUrl { get; set; }
    public string? Title { get; set; }
}
