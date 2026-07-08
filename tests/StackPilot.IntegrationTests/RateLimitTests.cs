using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class RateLimitEnabledFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"RateLimitTest_{Guid.NewGuid()}"));

            services.AddSingleton<IBackgroundJobService, NoOpBackgroundJobService>();
            services.AddSingleton<ICacheService, NoOpCacheService>();
        });
    }
}

public class RateLimitTests : IClassFixture<RateLimitEnabledFactory>
{
    private readonly RateLimitEnabledFactory _factory;

    public RateLimitTests(RateLimitEnabledFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthLogin_Returns429_AfterBurst()
    {
        var client = _factory.CreateClient();

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 15; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = $"ratelimit-{i}@example.com",
                password = "WrongPassword123!"
            });
            lastStatus = resp.StatusCode;
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}
