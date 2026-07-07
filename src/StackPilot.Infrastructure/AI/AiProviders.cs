using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackPilot.Application.AI;

namespace StackPilot.Infrastructure.AI;

public class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(HttpClient http, IConfiguration config, ILogger<OpenAiProvider> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public string ProviderName => "openai";

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OpenAI API key not configured");

        var model = request.Model ?? _config["Ai:OpenAI:Model"] ?? "gpt-4o";
        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API error: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"OpenAI API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = doc.RootElement.TryGetProperty("usage", out var usage) ? usage.GetProperty("total_tokens").GetInt32() : 0;

        return new AiCompletionResult { Content = content, Model = model, TokensUsed = tokens };
    }

    public async Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("OpenAI API key not configured");

        var model = request.Model ?? "text-embedding-3-small";
        var payload = new { model, input = request.Texts };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var embeddings = doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("embedding").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray())
            .ToList();

        return new AiEmbeddingResult { Embeddings = embeddings, TokensUsed = request.Texts.Count * 10 };
    }
}

public class AnthropicProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<AnthropicProvider> _logger;

    public AnthropicProvider(HttpClient http, IConfiguration config, ILogger<AnthropicProvider> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public string ProviderName => "anthropic";

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Anthropic API key not configured");

        var model = request.Model ?? _config["Ai:Anthropic:Model"] ?? "claude-sonnet-4-20250514";
        var payload = new
        {
            model,
            max_tokens = request.MaxTokens,
            system = request.SystemPrompt,
            messages = new[] { new { role = "user", content = request.UserPrompt } }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Anthropic API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
        var tokens = doc.RootElement.TryGetProperty("usage", out var usage)
            ? usage.GetProperty("input_tokens").GetInt32() + usage.GetProperty("output_tokens").GetInt32() : 0;

        return new AiCompletionResult { Content = content, Model = model, TokensUsed = tokens };
    }

    public Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("Anthropic does not provide an embedding API — use OpenAI for embeddings");
}

public class MockAiProvider : IAiProvider
{
    public string ProviderName => "mock";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var content = request.SystemPrompt.Contains("business analyst", StringComparison.OrdinalIgnoreCase)
            ? """{"businessSummary":"Automated analysis of the submitted ticket","functionalRequirements":"Implement the described feature with proper validation and error handling","nonFunctionalRequirements":"Response time < 200ms, 99.9% availability, secure by default","acceptanceCriteria":"Feature works as described\nAll tests pass\nNo security vulnerabilities","riskScore":4.5,"confidenceScore":0.82}"""
            : request.SystemPrompt.Contains("architect", StringComparison.OrdinalIgnoreCase)
            ? "## Implementation Plan\n\n### Affected Components\n- Service layer\n- API endpoints\n- Database schema\n\n### Steps\n1. Create feature branch\n2. Implement changes\n3. Add unit tests\n4. Update documentation\n\n### Rollback Plan\nRevert commit and redeploy previous version."
            : $"Based on the available context, here is my analysis regarding: {request.UserPrompt[..Math.Min(100, request.UserPrompt.Length)]}...";

        return Task.FromResult(new AiCompletionResult { Content = content, Model = "mock-gpt-4", TokensUsed = 500 });
    }

    public Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default)
    {
        var embeddings = request.Texts.Select(_ => Enumerable.Repeat(0.1f, 1536).ToArray()).ToList();
        return Task.FromResult(new AiEmbeddingResult { Embeddings = embeddings, TokensUsed = request.Texts.Count * 10 });
    }
}

public class CompositeAiProvider : IAiProvider
{
    private readonly IConfiguration _config;
    private readonly OpenAiProvider _openAi;
    private readonly AnthropicProvider _anthropic;
    private readonly MockAiProvider _mock;
    private readonly ILogger<CompositeAiProvider> _logger;

    public CompositeAiProvider(IConfiguration config, OpenAiProvider openAi, AnthropicProvider anthropic, MockAiProvider mock, ILogger<CompositeAiProvider> logger)
    {
        _config = config;
        _openAi = openAi;
        _anthropic = anthropic;
        _mock = mock;
        _logger = logger;
    }

    public string ProviderName => ResolveProvider().ProviderName;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var providers = GetProviderChain();
        foreach (var provider in providers)
        {
            try
            {
                return await provider.CompleteAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI provider {Provider} failed, trying next", provider.ProviderName);
            }
        }
        return await _mock.CompleteAsync(request, ct);
    }

    public async Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default)
    {
        try { return await _openAi.EmbedAsync(request, ct); }
        catch { return await _mock.EmbedAsync(request, ct); }
    }

    private IAiProvider ResolveProvider()
    {
        var configured = _config["Ai:Provider"]?.ToLowerInvariant() ?? "mock";
        return configured switch
        {
            "openai" => _openAi,
            "anthropic" => _anthropic,
            _ => _mock
        };
    }

    private List<IAiProvider> GetProviderChain()
    {
        var configured = _config["Ai:Provider"]?.ToLowerInvariant() ?? "mock";
        return configured switch
        {
            "openai" => [_openAi, _mock],
            "anthropic" => [_anthropic, _openAi, _mock],
            _ => [_mock]
        };
    }
}
