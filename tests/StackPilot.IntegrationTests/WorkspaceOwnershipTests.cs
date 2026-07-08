using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class WorkspaceOwnershipTests
{
    [Fact]
    public async Task CreateTicket_WithOtherOrgWorkspaceId_Is_NotAllowed()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        // Org A + userA
        var userAEmail = $"usera_ws_{Guid.NewGuid():N}@stackpilot.test";
        var (authA, orgAId) = await RegisterAndCreateOrg(client, userAEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgAId.ToString());
        var wsAId = await GetFirstWorkspaceId(client, orgAId);

        // Org B + userB
        var userBEmail = $"userb_ws_{Guid.NewGuid():N}@stackpilot.test";
        var (authB, orgBId) = await RegisterAndCreateOrg(client, userBEmail);

        // Invite userA into orgB so membership check passes.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());

        var inviteResponse = await client.PostAsJsonAsync($"/api/v1/organizations/{orgBId}/invites", new
        {
            email = userAEmail,
            roleName = "Developer"
        });
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);

        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationInviteCreatedDto>>();
        Assert.NotNull(inviteBody?.Data?.Token);

        // Accept invite as userA (bootstrap path: org header not required).
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");

        var acceptResponse = await client.PostAsJsonAsync("/api/v1/organizations/invites/accept", new
        {
            token = inviteBody!.Data!.Token
        });
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        // Now attempt to create a ticket in orgB context but using orgA workspaceId.
        using var baselineScope = factory.Services.CreateScope();
        var baselineDb = baselineScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var beforeCount = await baselineDb.Tickets
            .IgnoreQueryFilters()
            .CountAsync(t => t.WorkspaceId == wsAId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgBId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id"); // important: omit workspace header

        var createResponse = await client.PostAsJsonAsync($"/api/v1/workspaces/{wsAId}/tickets", new
        {
            title = "Cross-org workspace ownership test",
            description = "Should not allow creating a ticket in a workspace outside of X-Organization-Id context.",
            ticketType = "Enhancement",
            priority = "Medium",
            businessJustification = "Isolation regression test"
        });

        // We expect a hard failure (404/403/400) rather than success.
        Assert.NotEqual(HttpStatusCode.OK, createResponse.StatusCode);

        var baselineAfter = await baselineDb.Tickets
            .IgnoreQueryFilters()
            .CountAsync(t => t.WorkspaceId == wsAId);

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

    private static async Task<Guid> GetFirstWorkspaceId(HttpClient client, Guid orgId)
    {
        var wsResp = await client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        Assert.Equal(HttpStatusCode.OK, wsResp.StatusCode);
        var wsBody = await wsResp.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        return wsBody!.Data![0].Id;
    }
}

