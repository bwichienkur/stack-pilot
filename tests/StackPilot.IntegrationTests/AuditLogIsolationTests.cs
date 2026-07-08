using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Domain.Enums;

namespace StackPilot.IntegrationTests;

public class AuditLogIsolationTests
{
    [Fact]
    public async Task AuditLogs_CrossTenant_Is_Denied()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        // Org A + userA (creates an audit log by creating a ticket).
        var userAEmail = $"audit_a_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);

        SetAuthHeaders(client, authA, orgAId);
        var wsAId = await GetFirstWorkspaceId(client, orgAId);

        var ticketResp = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsAId}/tickets", new
        {
            title = "Audit log isolation test",
            description = "Ensure org B cannot read org A audit logs",
            ticketType = "Enhancement",
            priority = "Medium",
            businessJustification = "Isolation regression test"
        });
        Assert.Equal(HttpStatusCode.OK, ticketResp.StatusCode);

        // Org B + userB.
        var userBEmail = $"audit_b_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);
        SetAuthHeaders(client, authB, orgBId);

        // Attempt to read org A audit logs while operating under org B context.
        var auditResp = await client.GetAsync($"/api/v1/organizations/{orgAId}/audit-logs");
        Assert.Equal(HttpStatusCode.Unauthorized, auditResp.StatusCode);
    }

    private static void SetAuthHeaders(HttpClient client, string auth, Guid orgId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
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

