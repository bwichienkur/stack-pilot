using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class ConnectorOwnershipTests
{
    [Fact]
    public async Task CreateConnector_WithOtherOrgWorkspaceId_Is_NotAllowed()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        // Org A + userA (creates a workspace we will try to misuse)
        var userAEmail = $"usera_conn_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);
        var wsAId = await GetFirstWorkspaceId(client, authA, orgAId);

        // Org B + userB (will attempt to create connector in Org A workspace)
        var userBEmail = $"userb_conn_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);

        var baselineScope = factory.Services.CreateScope();
        var baselineDb = baselineScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var beforeCount = await baselineDb.ConnectorInstances
            .IgnoreQueryFilters()
            .CountAsync(c => c.WorkspaceId == wsAId);

        // Important: omit X-Workspace-Id to skip middleware workspace validation,
        // but keep X-Organization-Id so we exercise the fail-closed service check.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");

        var defsResponse = await client.GetAsync("/api/v1/connectors/definitions");
        defsResponse.EnsureSuccessStatusCode();
        var defsBody = await defsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ConnectorDefinitionDto>>>();
        var definitionId = defsBody!.Data![0].Id;

        var createResponse = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsAId}/connectors", new
        {
            name = "Cross-org connector ownership test",
            definitionId,
            configJson = "{}"
        });

        Assert.NotEqual(HttpStatusCode.OK, createResponse.StatusCode);

        var baselineAfter = await baselineDb.ConnectorInstances
            .IgnoreQueryFilters()
            .CountAsync(c => c.WorkspaceId == wsAId);

        Assert.Equal(beforeCount, baselineAfter);
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

