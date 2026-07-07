using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackPilot.Application.AI;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class SlackNotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<SlackNotificationService> _logger;

    public SlackNotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        AppDbContext db,
        ILogger<SlackNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _db = db;
        _logger = logger;
    }

    public async Task NotifyAsync(string eventType, string message, Guid? organizationId = null, CancellationToken ct = default)
    {
        var webhookUrl = await ResolveWebhookUrlAsync(organizationId, ct);
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;

        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new
            {
                text = $"*[StackPilot {eventType}]*\n{message}"
            };
            var response = await client.PostAsJsonAsync(webhookUrl, payload, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Slack notification failed: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Slack notification error for {EventType}", eventType);
        }
    }

    private async Task<string?> ResolveWebhookUrlAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is Guid orgId)
        {
            var org = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, ct);
            if (!string.IsNullOrEmpty(org?.SettingsJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(org.SettingsJson);
                    if (doc.RootElement.TryGetProperty("slackWebhookUrl", out var url))
                    {
                        var value = url.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
                catch { /* fall through */ }
            }
        }

        return _config["Notifications:Slack:WebhookUrl"];
    }
}
