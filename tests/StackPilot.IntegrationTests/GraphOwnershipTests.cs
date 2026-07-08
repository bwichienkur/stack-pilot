using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;

namespace StackPilot.IntegrationTests;

public class GraphOwnershipTests
{
    [Fact]
    public async Task ListGraphNodes_WithOtherOrgWorkspaceId_Is_NotAllowed()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        // Org A + userA
        var userAEmail = $"graph_a_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);
        var wsAId = await GetFirstWorkspaceId(client, authA, orgAId);

        // Org B + userB
        var userBEmail = $"graph_b_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);

        // Operate under org B context while routing to org A's workspace.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");

        var response = await client.GetAsync($"/api/v1/workspaces/{wsAId}/graph/nodes");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
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

    private static async Task<Guid> GetFirstWorkspaceId(HttpClient client, string auth, Guid orgId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var wsResp = await client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        Assert.Equal(HttpStatusCode.OK, wsResp.StatusCode);
        var wsBody = await wsResp.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        return wsBody!.Data![0].Id;
    }
}

