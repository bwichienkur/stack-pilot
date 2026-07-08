using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Application.Workflow;
using StackPilot.Domain.Entities;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.Services;

public class OutboundWebhookService : IOutboundWebhookService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OutboundWebhookService> _logger;

    public OutboundWebhookService(
        AppDbContext db,
        ITenantContext tenant,
        IHttpClientFactory httpClientFactory,
        ILogger<OutboundWebhookService> logger)
    {
        _db = db;
        _tenant = tenant;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OutboundWebhookSubscriptionDto> CreateAsync(Guid organizationId, CreateOutboundWebhookRequest request, CancellationToken ct = default)
    {
        _tenant.SetOrganization(organizationId);

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var subscription = new OutboundWebhookSubscription
        {
            OrganizationId = organizationId,
            Url = request.Url,
            Secret = secret,
            EventsJson = JsonSerializer.Serialize(request.Events),
            IsActive = true
        };

        _db.OutboundWebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return Map(subscription);
    }

    public async Task<List<OutboundWebhookSubscriptionDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.SetOrganization(organizationId);
        var subscriptions = await _db.OutboundWebhookSubscriptions
            .Where(s => s.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return subscriptions.Select(Map).ToList();
    }

    public async Task DeleteAsync(Guid organizationId, Guid subscriptionId, CancellationToken ct = default)
    {
        _tenant.SetOrganization(organizationId);
        var subscription = await _db.OutboundWebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct)
            ?? throw new KeyNotFoundException("Subscription not found");

        _db.OutboundWebhookSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DispatchAsync(string eventType, Guid organizationId, object payload, CancellationToken ct = default)
    {
        var subscriptions = await _db.OutboundWebhookSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId && s.IsActive)
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var body = JsonSerializer.Serialize(new { eventType, organizationId, payload, timestamp = DateTime.UtcNow });
        var client = _httpClientFactory.CreateClient();

        foreach (var subscription in subscriptions)
        {
            var events = JsonSerializer.Deserialize<string[]>(subscription.EventsJson) ?? [];
            if (events.Length > 0 && !events.Contains(eventType, StringComparer.OrdinalIgnoreCase) && !events.Contains("*"))
                continue;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add("X-StackPilot-Event", eventType);
                request.Headers.Add("X-StackPilot-Signature", StackPilotWebhookSignature.Compute(body, subscription.Secret));

                var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    _logger.LogWarning("Outbound webhook to {Url} returned {Status}", subscription.Url, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbound webhook to {Url}", subscription.Url);
            }
        }
    }

    private static OutboundWebhookSubscriptionDto Map(OutboundWebhookSubscription s) =>
        new(s.Id, s.Url, JsonSerializer.Deserialize<string[]>(s.EventsJson) ?? [], s.IsActive, s.CreatedAt);
}
