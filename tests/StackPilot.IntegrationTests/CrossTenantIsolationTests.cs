using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Domain.Entities;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class CrossTenantIsolationTests
{
    [Fact]
    public async Task GetTicket_CrossTenant_Returns_NotFound()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        var userAEmail = $"cta_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgAId.ToString());
        var wsAId = await GetFirstWorkspaceId(client, orgAId);
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", wsAId.ToString());

        var ticketResponse = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsAId}/tickets", new
        {
            title = "Cross-tenant read test",
            ticketType = "Enhancement",
            priority = "Medium"
        });
        Assert.Equal(HttpStatusCode.OK, ticketResponse.StatusCode);
        var ticketId = (await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>())!.Data!.Id;

        var userBEmail = $"ctb_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");

        var getResponse = await client.GetAsync($"/api/v1/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GeneratePlan_CrossTenant_IsForbidden()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        var userAEmail = $"plana_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgAId.ToString());
        var wsAId = await GetFirstWorkspaceId(client, orgAId);
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", wsAId.ToString());

        var ticketResponse = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsAId}/tickets", new
        {
            title = "Cross-tenant plan test",
            ticketType = "Enhancement",
            priority = "Medium"
        });
        var ticketId = (await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>())!.Data!.Id;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.IgnoreQueryFilters().FirstAsync(t => t.Id == ticketId);
            ticket.Status = TicketStatus.Approved;
            await db.SaveChangesAsync();
        }

        var userBEmail = $"planb_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");

        var planResponse = await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-plan", null);
        Assert.NotEqual(HttpStatusCode.OK, planResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidWorkspaceHeader_Returns_Forbidden()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        var userAEmail = $"wsa_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);

        var userBEmail = $"wsb_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        var wsBId = await GetFirstWorkspaceId(client, orgBId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgAId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", wsBId.ToString());

        var response = await client.GetAsync($"/api/v1/organizations/{orgAId}/workspaces");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AiTokenLimit_Blocks_GenerateRequirements()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"aitoken_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(client, email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiActions.Add(new AiAction
            {
                OrganizationId = orgId,
                ActionType = "chat",
                Model = "test",
                TokensUsed = 100_000,
                Status = AiActionStatus.Completed,
                IsReversible = true
            });
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        var wsId = await GetFirstWorkspaceId(client, orgId);
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", wsId.ToString());

        var ticketResponse = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsId}/tickets", new
        {
            title = "AI token limit test",
            ticketType = "Enhancement",
            priority = "Medium"
        });
        Assert.Equal(HttpStatusCode.OK, ticketResponse.StatusCode);
        var ticketId = (await ticketResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>())!.Data!.Id;

        var reqResponse = await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        Assert.Equal(HttpStatusCode.PaymentRequired, reqResponse.StatusCode);

        var error = await reqResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("AI_TOKEN_LIMIT", error!.Errors![0].Code);
    }

    private static async Task<(string Auth, Guid OrgId)> RegisterAndCreateOrg(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var auth = registerBody!.Data!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);

        var orgResponse = await client.PostAsJsonAsync("/api/v1/organizations", new
        {
            name = "Test Org",
            slug = $"test-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);

        var orgBody = await orgResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationCreatedDto>>();
        return (orgBody!.Data!.AccessToken, orgBody.Data!.Organization.Id);
    }

    private static async Task<Guid> GetFirstWorkspaceId(HttpClient client, Guid orgId)
    {
        var wsResp = await client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        Assert.Equal(HttpStatusCode.OK, wsResp.StatusCode);
        var wsBody = await wsResp.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        return wsBody!.Data![0].Id;
    }
}
