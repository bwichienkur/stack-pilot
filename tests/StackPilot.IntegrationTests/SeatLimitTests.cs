using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using StackPilot.Application.Common;
using StackPilot.Application.DTOs;
using StackPilot.Domain.Enums;

namespace StackPilot.IntegrationTests;

public class SeatLimitTests
{
    private const string Password = "TestPassword123!";

    [Fact]
    public async Task SeatLimit_On_Trial_Returns_402()
    {
        using var factory = new StackPilotWebApplicationFactory();
        var client = factory.CreateClient();

        // Org defaults to Trial plan (max 5 seats: owner + 4 members).
        var adminEmail = $"seat_admin_{Guid.NewGuid():N}@stackpilot.test";
        var (adminAuth, orgId) = await RegisterAndCreateOrg(client, adminEmail);

        SetAuthHeaders(client, adminAuth, orgId);

        // Fill remaining seats (4 more users).
        var acceptedUsers = 0;
        for (var i = 0; i < 4; i++)
        {
            // Switch back to org admin for creating the next invite.
            SetAuthHeaders(client, adminAuth, orgId);

            var inviteeEmail = $"seat_invitee_{Guid.NewGuid():N}@stackpilot.test";
            var inviteResp = await client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/invites", new
            {
                email = inviteeEmail,
                roleName = "Developer"
            });
            Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);

            var inviteBody =
                await inviteResp.Content.ReadFromJsonAsync<ApiResponse<OrganizationInviteCreatedDto>>();
            Assert.NotNull(inviteBody?.Data?.Token);

            var inviteeAuth = await RegisterAndGetToken(client, inviteeEmail);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", inviteeAuth);
            client.DefaultRequestHeaders.Remove("X-Organization-Id");

            var acceptResp = await client.PostAsJsonAsync("/api/v1/organizations/invites/accept", new
            {
                token = inviteBody!.Data!.Token
            });

            Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);
            acceptedUsers++;
        }

        Assert.Equal(4, acceptedUsers);

        // Next invite should fail at seat enforcement.
        SetAuthHeaders(client, adminAuth, orgId);
        var overflowEmail = $"seat_overflow_{Guid.NewGuid():N}@stackpilot.test";
        var overflowInvite = await client.PostAsJsonAsync($"/api/v1/organizations/{orgId}/invites", new
        {
            email = overflowEmail,
            roleName = "Developer"
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, overflowInvite.StatusCode);

        var error = await overflowInvite.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(error?.Errors);
        Assert.NotEmpty(error!.Errors!);
        Assert.Equal("SEAT_LIMIT", error!.Errors![0].Code);
    }

    private static void SetAuthHeaders(HttpClient client, string auth, Guid orgId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());
        client.DefaultRequestHeaders.Remove("X-Workspace-Id");
    }

    private static async Task<string> RegisterAndGetToken(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            firstName = "Test",
            lastName = "User"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private static async Task<(string Auth, Guid OrgId)> RegisterAndCreateOrg(HttpClient client, string email)
    {
        var auth = await RegisterAndGetToken(client, email);
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
}

