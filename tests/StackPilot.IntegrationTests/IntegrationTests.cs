using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Enums;
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

            var tenantDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantContext));
            if (tenantDescriptor is not null) services.Remove(tenantDescriptor);

            services.AddScoped<ITenantContext>(sp =>
            {
                var ctx = new TenantContext();
                ctx.DisableTenantFilter();
                return ctx;
            });

            services.AddSingleton<IBackgroundJobService, NoOpBackgroundJobService>();
        });
    }
}

public class NoOpBackgroundJobService : IBackgroundJobService
{
    public string EnqueueConnectorSync(Guid connectorId) => Guid.NewGuid().ToString();
    public string EnqueueRepositoryScan(Guid connectorId, string repositoryName) => Guid.NewGuid().ToString();
    public string EnqueueDatabaseScan(Guid connectorId, string databaseName) => Guid.NewGuid().ToString();
    public string EnqueueGenerateRequirements(Guid ticketId) => Guid.NewGuid().ToString();
}

public class AuthIntegrationTests : IClassFixture<StackPilotWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly StackPilotWebApplicationFactory _factory;

    public AuthIntegrationTests(StackPilotWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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
    public async Task RefreshToken_Rotates_And_Returns_New_AccessToken()
    {
        var email = $"refresh_{Guid.NewGuid():N}@stackpilot.test";
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var refreshToken = registerBody!.Data!.RefreshToken;
        Assert.False(string.IsNullOrEmpty(refreshToken));

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(refreshBody?.Data?.AccessToken);
        Assert.NotEqual(refreshToken, refreshBody.Data.RefreshToken);
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
        Assert.Contains(body.Data!, d => d.Type == "servicenow");
        Assert.Contains(body.Data!, d => d.Type == "azure_devops_repository");
        Assert.Contains(body.Data!, d => d.Type == "mysql");
        Assert.All(body.Data!, d => Assert.False(string.IsNullOrWhiteSpace(d.Category)));
    }

    [Fact]
    public async Task ConnectorDefinitions_AreCategorized()
    {
        var email = $"cat_{Guid.NewGuid():N}@stackpilot.test";
        var auth = await RegisterAndGetToken(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);

        var response = await _client.GetAsync("/api/v1/connectors/definitions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConnectorDefinitionDto>>>();
        var jira = body!.Data!.First(d => d.Type == "jira");
        var github = body.Data!.First(d => d.Type == "github_repository");
        var sql = body.Data!.First(d => d.Type == "sql_server");

        Assert.Equal("Itsm", jira.Category);
        Assert.Equal("SourceCode", github.Category);
        Assert.Equal("Data", sql.Category);
    }

    [Fact]
    public async Task CreateOrganization_Includes_Default_Workspace_And_Upgrade_Allows_More()
    {
        var email = $"org_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(email);
        SetAuthHeaders(auth, orgId);

        var listResponse = await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        Assert.Single(listBody!.Data!);

        var blocked = await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new { name = "Dev", slug = "dev" });
        Assert.Equal(HttpStatusCode.PaymentRequired, blocked.StatusCode);

        await _client.PostAsJsonAsync($"/api/v1/billing/organizations/{orgId}/checkout", new
        {
            plan = "Starter",
            billingInterval = "monthly",
            successUrl = "https://app.stackpilot.test/settings",
            cancelUrl = "https://app.stackpilot.test/pricing"
        });

        var wsResponse = await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new { name = "Dev", slug = "dev" });
        Assert.Equal(HttpStatusCode.OK, wsResponse.StatusCode);
    }

    [Fact]
    public async Task TicketWorkflow_Create_And_GenerateRequirements()
    {
        var email = $"ticket_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResponse = await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
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

    [Fact]
    public async Task GoldenPath_Register_Org_Ticket_Requirements_Approval()
    {
        var email = $"golden_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResponse = await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        var wsBody = await wsResponse.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketResponse = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "Golden path feature",
            description = "End-to-end validation",
            ticketType = "Enhancement",
            priority = "High",
            businessJustification = "CI golden path"
        });
        Assert.Equal(HttpStatusCode.OK, ticketResponse.StatusCode);
        var ticketBody = await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>();
        var ticketId = ticketBody!.Data!.Id;

        var reqResponse = await _client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        Assert.Equal(HttpStatusCode.OK, reqResponse.StatusCode);

        var pendingResponse = await _client.GetAsync($"/api/v1/workspaces/{workspaceId}/approvals/pending");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pendingBody = await pendingResponse.Content.ReadFromJsonAsync<ApiResponse<List<TicketDto>>>();
        Assert.Contains(pendingBody!.Data!, t => t.Id == ticketId);

        var approveResponse = await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/approvals", new
        {
            approvalType = "TechnicalReviewer",
            decision = "Approved",
            comments = "LGTM"
        });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
    }

    [Fact]
    public async Task Qa_And_Uat_Queues_Work()
    {
        var email = $"qauat_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsBody = await (await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces")).Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketResponse = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "QA UAT test",
            ticketType = "Bug",
            priority = "Low"
        });
        var ticketId = (await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>())!.Data!.Id;

        await _client.PatchAsJsonAsync($"/api/v1/tickets/{ticketId}", new { status = "DeployedToTest" });

        var qaPending = await _client.GetAsync($"/api/v1/workspaces/{workspaceId}/qa/pending");
        Assert.Equal(HttpStatusCode.OK, qaPending.StatusCode);

        var qaSubmit = await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/qa", new { result = "pass", notes = "All tests pass" });
        Assert.Equal(HttpStatusCode.OK, qaSubmit.StatusCode);

        var uatPending = await _client.GetAsync($"/api/v1/workspaces/{workspaceId}/uat/pending");
        Assert.Equal(HttpStatusCode.OK, uatPending.StatusCode);

        var uatSubmit = await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/uat", new { decision = "Approved", comments = "Accepted" });
        Assert.Equal(HttpStatusCode.OK, uatSubmit.StatusCode);
    }

    [Fact]
    public async Task ScheduledReleases_List_Returns_For_Workspace()
    {
        var email = $"release_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(email);

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsBody = await (await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces")).Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketResponse = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "Release test ticket",
            ticketType = "Enhancement",
            priority = "Medium"
        });
        var ticketId = (await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>())!.Data!.Id;

        await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/qa", new { result = "pass", notes = "Ready for UAT" });
        await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/uat", new { decision = "Approved", comments = "Accepted for release" });

        var scheduleResponse = await _client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/schedule-release", new
        {
            scheduledAt = DateTime.UtcNow.AddDays(3),
            releaseWindow = "Weekend maintenance",
            rollbackPlan = "Revert commit"
        });
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);

        var listResponse = await _client.GetAsync($"/api/v1/workspaces/{workspaceId}/releases");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ReleaseScheduleDetailDto>>>();
        Assert.Contains(listBody!.Data!, r => r.TicketId == ticketId);
    }

    [Fact]
    public async Task BillingPlans_Returns_All_Tiers()
    {
        var response = await _client.GetAsync("/api/v1/billing/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<PlanPricingDto>>>();
        Assert.Equal(4, body!.Data!.Count);
        Assert.Contains(body.Data, p => p.Plan == "Starter" && p.MonthlyPriceUsd == 349m);
        Assert.Contains(body.Data, p => p.Plan == "Enterprise" && p.MonthlyPriceUsd == null);
    }

    [Fact]
    public async Task BillingOrganization_Returns_Enforcement_Fields()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"billing_{Guid.NewGuid():N}@stackpilot.test");
        SetAuthHeaders(auth, orgId);

        var response = await _client.GetAsync($"/api/v1/billing/organizations/{orgId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationBillingDto>>();
        Assert.Equal("Trial", body!.Data!.Plan);
        Assert.False(body.Data.IsWriteBlocked);
        Assert.Null(body.Data.BlockReason);
        Assert.Equal(3, body.Data.Limits.MaxConnectors);
    }

    [Fact]
    public async Task WorkspaceLimit_On_Trial_Returns_402()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"wslimit_{Guid.NewGuid():N}@stackpilot.test");
        SetAuthHeaders(auth, orgId);

        var response = await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new
        {
            name = "Second workspace",
            slug = $"ws-{Guid.NewGuid():N}",
            description = "Should exceed trial limit"
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("WORKSPACE_LIMIT", error!.Errors![0].Code);
    }

    [Fact]
    public async Task ConnectorLimit_On_Trial_Returns_402()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"conlimit_{Guid.NewGuid():N}@stackpilot.test");
        SetAuthHeaders(auth, orgId);

        var wsBody = await (await _client.GetAsync($"/api/v1/organizations/{orgId}/workspaces"))
            .Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        _client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var defs = await (await _client.GetAsync("/api/v1/connectors/definitions"))
            .Content.ReadFromJsonAsync<ApiResponse<List<ConnectorDefinitionDto>>>();
        var definitionId = defs!.Data![0].Id;

        for (var i = 0; i < 3; i++)
        {
            var ok = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/connectors", new
            {
                name = $"Connector {i + 1}",
                definitionId,
                configJson = "{}"
            });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var blocked = await _client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/connectors", new
        {
            name = "Connector 4",
            definitionId,
            configJson = "{}"
        });
        Assert.Equal(HttpStatusCode.PaymentRequired, blocked.StatusCode);
        var error = await blocked.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("CONNECTOR_LIMIT", error!.Errors![0].Code);
    }

    [Fact]
    public async Task TrialExpired_Blocks_Writes()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"expired_{Guid.NewGuid():N}@stackpilot.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await db.Organizations.FindAsync(orgId);
        org!.TrialEndsAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        SetAuthHeaders(auth, orgId);

        var response = await _client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/workspaces", new
        {
            name = "Blocked workspace",
            slug = $"blocked-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("SUBSCRIPTION_BLOCKED", error!.Errors![0].Code);
    }

    [Fact]
    public async Task BillingPortal_Returns_Mock_When_Stripe_Not_Configured()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"portal_{Guid.NewGuid():N}@stackpilot.test");
        SetAuthHeaders(auth, orgId);

        var response = await _client.PostAsJsonAsync($"/api/v1/billing/organizations/{orgId}/portal", new
        {
            returnUrl = "https://app.stackpilot.test/settings"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PortalSessionDto>>();
        Assert.True(body!.Data!.IsMock);
        Assert.Contains("portal=mock", body.Data.Url);
    }

    [Fact]
    public async Task MockCheckout_Upgrades_Plan()
    {
        var (auth, orgId) = await RegisterAndCreateOrg($"checkout_{Guid.NewGuid():N}@stackpilot.test");
        SetAuthHeaders(auth, orgId);

        var checkout = await _client.PostAsJsonAsync($"/api/v1/billing/organizations/{orgId}/checkout", new
        {
            plan = "Starter",
            billingInterval = "monthly",
            successUrl = "https://app.stackpilot.test/settings",
            cancelUrl = "https://app.stackpilot.test/pricing",
            promotionCode = "DESIGNPARTNER20"
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var session = await checkout.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionDto>>();
        Assert.True(session!.Data!.IsMock);

        var billing = await (await _client.GetAsync($"/api/v1/billing/organizations/{orgId}"))
            .Content.ReadFromJsonAsync<ApiResponse<OrganizationBillingDto>>();
        Assert.Equal("Starter", billing!.Data!.Plan);
        Assert.Equal(SubscriptionStatus.Active.ToString(), billing.Data.SubscriptionStatus);
    }

    [Fact]
    public async Task CrossTenant_Access_Is_Denied()
    {
        var userA = await RegisterAndCreateOrg($"usera_{Guid.NewGuid():N}@stackpilot.test");
        var userB = await RegisterAndCreateOrg($"userb_{Guid.NewGuid():N}@stackpilot.test");

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userA.Auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", userB.OrgId.ToString());

        var response = await _client.GetAsync($"/api/v1/organizations/{userB.OrgId}/workspaces");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private void SetAuthHeaders(string auth, Guid orgId)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);
        _client.DefaultRequestHeaders.Remove("X-Organization-Id");
        _client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        _client.DefaultRequestHeaders.Remove("X-Workspace-Id");
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

    private async Task<(string Auth, Guid OrgId)> RegisterAndCreateOrg(string email)
    {
        var auth = await RegisterAndGetToken(email);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth);

        var orgResponse = await _client.PostAsJsonAsync("/api/v1/organizations", new { name = "Test Org", slug = $"test-{Guid.NewGuid():N}" });
        var orgBody = await orgResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationCreatedDto>>();
        return (orgBody!.Data!.AccessToken, orgBody.Data.Organization.Id);
    }
}
