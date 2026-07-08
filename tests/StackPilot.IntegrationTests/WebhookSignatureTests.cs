using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.Interfaces;
using StackPilot.Application.Workflow;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class WebhookSignatureTests
{
    private static string SignPayload(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return $"sha256={signature}";
    }

    [Fact]
    public async Task GitHubWebhook_SecretConfigured_SignatureMissing_Returns_Unauthorized()
    {
        const string secret = "testsecret";
        Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", secret);

        try
        {
            using var factory = new StackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = "{\"hello\":\"world\"}";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/github")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-GitHub-Event", "ping");

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", null);
        }
    }

    [Fact]
    public async Task GitHubWebhook_SecretConfigured_SignatureValid_Returns_Ok()
    {
        const string secret = "testsecret";
        Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", secret);

        try
        {
            using var factory = new StackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = "{\"hello\":\"world\"}";
            var signature = SignPayload(payload, secret);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/github")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-GitHub-Event", "ping");
            request.Headers.Add("X-Hub-Signature-256", signature);

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", null);
        }
    }

    [Fact]
    public async Task GitHubWebhook_SecretMissing_InProduction_FailsClosed()
    {
        Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", null);

        try
        {
            using var factory = new ProductionStackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = "{\"hello\":\"world\"}";

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/webhooks/github")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-GitHub-Event", "ping");

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Webhooks__GitHub__Secret", null);
        }
    }

    [Fact]
    public async Task StripeWebhook_SecretConfigured_SignatureMissing_Returns_Unauthorized()
    {
        const string webhookSecret = "integration_test_webhook_signing_secret";
        Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", webhookSecret);
        Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", "sk_test_placeholder");

        try
        {
            using var factory = new StackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = """{"type":"ping"}""";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/stripe")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", null);
            Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", null);
        }
    }

    [Fact]
    public async Task StripeWebhook_SecretConfigured_SignatureValid_Returns_Ok()
    {
        const string webhookSecret = "integration_test_webhook_signing_secret";
        Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", webhookSecret);
        Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", "sk_test_placeholder");

        try
        {
            using var factory = new StackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = """{"id":"evt_test","object":"event","api_version":"2020-08-27","created":1609459200,"type":"product.created","data":{"object":{"id":"prod_test","object":"product","active":true}}}""";
            var signature = StripeWebhookSignature.Sign(payload, webhookSecret);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/stripe")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Stripe-Signature", signature);

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", null);
            Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", null);
        }
    }

    [Fact]
    public async Task StripeWebhook_SecretMissing_InProduction_FailsClosed()
    {
        Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", null);
        Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", null);

        try
        {
            using var factory = new ProductionStackPilotWebApplicationFactory();
            var client = factory.CreateClient();

            var payload = """{"type":"ping"}""";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhooks/stripe")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Billing__Stripe__WebhookSecret", null);
            Environment.SetEnvironmentVariable("Billing__Stripe__SecretKey", null);
        }
    }
}

public class ProductionStackPilotWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"StackPilotTestProd_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            var tenantDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantContext));
            if (tenantDescriptor is not null) services.Remove(tenantDescriptor);

            services.AddScoped<ITenantContext>(sp =>
            {
                return new TenantContext();
            });

            services.AddSingleton<IBackgroundJobService, NoOpBackgroundJobService>();

            var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICacheService));
            if (cacheDescriptor is not null) services.Remove(cacheDescriptor);
            services.AddSingleton<ICacheService, NoOpCacheService>();
        });
    }
}

