using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class StackPilotWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"StackPilotTest_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.AddSingleton<IBackgroundJobService, NoOpBackgroundJobService>();
        });
    }
}

public class NoOpBackgroundJobService : StackPilot.Application.Interfaces.IBackgroundJobService
{
    public string EnqueueConnectorSync(Guid connectorId) => Guid.NewGuid().ToString();
    public string EnqueueRepositoryScan(Guid connectorId, string repositoryName) => Guid.NewGuid().ToString();
    public string EnqueueDatabaseScan(Guid connectorId, string databaseName) => Guid.NewGuid().ToString();
    public string EnqueueGenerateRequirements(Guid ticketId) => Guid.NewGuid().ToString();
}

public class AuthIntegrationTests : IClassFixture<StackPilotWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(StackPilotWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Register_And_Login_ReturnsToken()
    {
        var email = $"test_{Guid.NewGuid():N}@stackpilot.test";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(registerBody?.Data?.AccessToken);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "TestPassword123!"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(loginBody?.Data?.AccessToken);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConnectorDefinitions_IncludesGitLab()
    {
        var email = $"admin_{Guid.NewGuid():N}@stackpilot.test";
        var auth = await RegisterAndGetToken(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);

        var response = await _client.GetAsync("/api/v1/connectors/definitions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConnectorDefinitionDto>>>();
        Assert.Contains(body!.Data!, d => d.Type == "gitlab_repository");
        Assert.Contains(body.Data!, d => d.Type == "github_repository");
    }

    [Fact]
    public async Task CreateOrganization_And_Workspace_Succeeds()
    {
        var email = $"org_{Guid.NewGuid():N}@stackpilot.test";
        var auth = await RegisterAndGetToken(email);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);

        var orgResponse = await _client.PostAsJsonAsync("/api/v1/organizations", new { name = "Test Org", slug = $"test-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);

        var orgBody = await orgResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationDto>>();
        var orgId = orgBody!.Data!.Id;

        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResponse = await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new { name = "Dev", slug = "dev" });
        Assert.Equal(HttpStatusCode.OK, wsResponse.StatusCode);
    }

    [Fact]
    public async Task TicketWorkflow_Create_And_GenerateRequirements()
    {
        var email = $"ticket_{Guid.NewGuid():N}@stackpilot.test";
        var auth = await RegisterAndGetToken(email);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);

        var orgId = await CreateOrgAndWorkspace();
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResponse = await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketResponse = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "Add email validation",
            description = "Users need email validation on signup",
            ticketType = "Enhancement",
            priority = "Medium",
            businessJustification = "Reduce invalid accounts"
        });

        Assert.Equal(HttpStatusCode.OK, ticketResponse.StatusCode);
        var ticketBody = await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>();
        var ticketId = ticketBody!.Data!.Id;

        var reqResponse = await _client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        Assert.Equal(HttpStatusCode.OK, reqResponse.StatusCode);
    }

    private async Task<string> RegisterAndGetToken(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private async Task<Guid> CreateOrgAndWorkspace()
    {
        var orgResponse = await _client.PostAsJsonAsync("/api/v1/organizations", new { name = "Test Org", slug = $"test-{Guid.NewGuid():N}" });
        var orgBody = await orgResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationDto>>();
        var orgId = orgBody!.Data!.Id;

        await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new { name = "Default", slug = "default" });
        return orgId;
    }
}
