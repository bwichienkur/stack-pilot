using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;

namespace StackPilot.IntegrationTests;

public class AiWorkflowTests
{
    [Fact]
    public async Task GenerateCode_ReturnsStructuredFiles_WhenApproved()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"codegen_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(client, email);
        var workspaceId = await GetWorkspaceId(client, orgId, auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketId = await CreateTicket(client, workspaceId);
        await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        await ApproveTicket(client, ticketId);
        await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-plan", null);

        var genResp = await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-code", null);
        Assert.Equal(HttpStatusCode.OK, genResp.StatusCode);

        var body = await genResp.Content.ReadFromJsonAsync<ApiResponse<AiCodeSuggestionDto>>();
        Assert.NotNull(body?.Data);
        Assert.NotNull(body.Data.Files);
        Assert.NotEmpty(body.Data.Files);
        Assert.False(string.IsNullOrWhiteSpace(body.Data.SuggestedCode));
    }

    [Fact]
    public async Task CreateBranch_Simulated_WhenNoGitHubConnector()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"branch_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(client, email);
        var workspaceId = await GetWorkspaceId(client, orgId, auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketId = await CreateTicket(client, workspaceId);
        await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        await ApproveTicket(client, ticketId);

        var actionResp = await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/ai-workflow/create_branch", new { });
        Assert.Equal(HttpStatusCode.OK, actionResp.StatusCode);

        var body = await actionResp.Content.ReadFromJsonAsync<ApiResponse<AiWorkflowActionResultDto>>();
        Assert.True(body?.Data?.Success);
        Assert.Equal("create_branch", body.Data.ActionType);
    }

    [Fact]
    public async Task ReverseAiAction_MarksActionReversed()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"reverse_{Guid.NewGuid():N}@stackpilot.test";
        var (auth, orgId) = await RegisterAndCreateOrg(client, email);
        var workspaceId = await GetWorkspaceId(client, orgId, auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        var ticketId = await CreateTicket(client, workspaceId);
        await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-requirements", null);
        await ApproveTicket(client, ticketId);

        var actionResp = await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/ai-workflow/create_branch", new { });
        actionResp.EnsureSuccessStatusCode();
        var actionBody = await actionResp.Content.ReadFromJsonAsync<ApiResponse<AiWorkflowActionResultDto>>();
        var actionId = actionBody!.Data!.ActionId;

        var reverseResp = await client.PostAsync($"/api/v1/ai-actions/{actionId}/reverse", null);
        Assert.Equal(HttpStatusCode.OK, reverseResp.StatusCode);

        var reverseAgain = await client.PostAsync($"/api/v1/ai-actions/{actionId}/reverse", null);
        Assert.Equal(HttpStatusCode.BadRequest, reverseAgain.StatusCode);
    }

    private static async Task<(string Auth, Guid OrgId)> RegisterAndCreateOrg(HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });
        var registerBody = await register.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(registerBody?.Data?.AccessToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerBody!.Data!.AccessToken);

        var orgResp = await client.PostAsJsonAsync("/api/v1/organizations", new
        {
            name = "Test Org",
            slug = $"test-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);

        var orgBody = await orgResp.Content.ReadFromJsonAsync<ApiResponse<OrganizationCreatedDto>>();
        Assert.NotNull(orgBody?.Data?.AccessToken);

        return (orgBody!.Data!.AccessToken, orgBody.Data.Organization.Id);
    }

    private static async Task<Guid> GetWorkspaceId(HttpClient client, Guid orgId, string auth)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResp = await client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        wsResp.EnsureSuccessStatusCode();
        var wsBody = await wsResp.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        return wsBody!.Data![0].Id;
    }

    private static async Task<Guid> CreateTicket(HttpClient client, Guid workspaceId)
    {
        var ticketResp = await client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "Workflow test ticket",
            description = "AI workflow integration test",
            ticketType = "Enhancement",
            priority = "Medium",
            businessJustification = "Test"
        });
        if (!ticketResp.IsSuccessStatusCode)
        {
            var error = await ticketResp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Create ticket failed: {(int)ticketResp.StatusCode} {error}");
        }
        var ticketBody = await ticketResp.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>();
        return ticketBody!.Data!.Id;
    }

    private static async Task ApproveTicket(HttpClient client, Guid ticketId)
    {
        var approvalResp = await client.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/approvals", new
        {
            approvalType = "TechnicalReviewer",
            decision = "Approved",
            comments = "approved for test"
        });
        approvalResp.EnsureSuccessStatusCode();
    }
}
