using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Domain.Enums;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.IntegrationTests;

public class AiGovernanceTests
{
    [Fact]
    public async Task GenerateCode_RequiresHumanApproval_WhenGovernanceEnabled()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"codegov_{Guid.NewGuid():N}@stackpilot.test";
        var (orgAccessToken, orgId) = await RegisterAndCreateOrg(client, email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", orgAccessToken);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        // Resolve workspace id for tenant + workspace validation.
        var wsResp = await client.GetAsync($"/api/v1/organizations/{orgId}/workspaces");
        Assert.Equal(HttpStatusCode.OK, wsResp.StatusCode);
        var wsBody = await wsResp.Content.ReadFromJsonAsync<ApiResponse<List<WorkspaceDto>>>();
        var workspaceId = wsBody!.Data![0].Id;

        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
        client.DefaultRequestHeaders.Add("X-Workspace-Id", workspaceId.ToString());

        // Create ticket (no approvals yet).
        var ticketResp = await client.PostAsJsonAsync($"/api/v1/workspaces/{workspaceId}/tickets", new
        {
            title = "Generate code governance test",
            description = "We should block code generation unless a human approval record exists.",
            ticketType = "Enhancement",
            priority = "Medium",
            businessJustification = "Security regression test"
        });
        Assert.Equal(HttpStatusCode.OK, ticketResp.StatusCode);
        var ticketBody = await ticketResp.Content.ReadFromJsonAsync<ApiResponse<TicketDto>>();
        var ticketId = ticketBody!.Data!.Id;

        // Force ticket status to Approved WITHOUT creating an approval record.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ticket = await db.Tickets.IgnoreQueryFilters().FirstAsync(t => t.Id == ticketId);
            ticket.Status = TicketStatus.Approved;
            await db.SaveChangesAsync();
        }

        // Governance requires human approval for "generate_code", so this should fail closed.
        using var genResp = await client.PostAsync($"/api/v1/tickets/{ticketId}/generate-code", null);
        Assert.Equal(HttpStatusCode.Unauthorized, genResp.StatusCode);
    }

    private static async Task<(string OrgAccessToken, Guid OrgId)> RegisterAndCreateOrg(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "TestPassword123!",
            firstName = "Test",
            lastName = "User"
        });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        Assert.NotNull(registerBody?.Data?.AccessToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerBody!.Data!.AccessToken);

        var orgResponse = await client.PostAsJsonAsync("/api/v1/organizations", new
        {
            name = "Test Org",
            slug = $"test-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);

        var orgBody = await orgResponse.Content.ReadFromJsonAsync<ApiResponse<OrganizationCreatedDto>>();
        Assert.NotNull(orgBody?.Data?.AccessToken);

        return (orgBody!.Data!.AccessToken, orgBody.Data!.Organization.Id);
    }
}

