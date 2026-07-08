import { test, expect } from "@playwright/test";
import { buildUniqueEmail, createOrganization, expectOk, orgHeaders, registerViaApi, registerViaUi } from "./helpers";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";

test.describe("Invite accept flow", () => {
  test.describe.configure({ retries: 0 });

  test("create invite, register invitee, accept via UI", async ({ page, request }) => {
    const inviterEmail = buildUniqueEmail("inviter");
    const inviteeEmail = buildUniqueEmail("invitee");
    const password = "TestPassword123!";

    const inviter = await registerViaApi(request, {
      email: inviterEmail,
      password,
      firstName: "Inviter",
      lastName: "User",
    });

    const { orgId, orgToken } = await createOrganization(request, inviter.token, {
      name: "Invite Org",
      slug: `inv-${Date.now()}`,
    });

    const inviteRes = await request.post(`${apiBase}/organizations/${orgId}/invites`, {
      headers: orgHeaders(orgToken, orgId),
      data: { email: inviteeEmail, roleName: "Developer" },
    });
    await expectOk(inviteRes, "Create invite API");
    const inviteBody = await inviteRes.json();
    const inviteUrl = inviteBody.data.inviteUrl as string;

    await registerViaUi(page, {
      firstName: "Invited",
      lastName: "User",
      email: inviteeEmail,
      password,
    });
    await expect(page.getByText("Step 1 of 4: Organization")).toBeVisible({ timeout: 15000 });

    const invitePath = new URL(inviteUrl).pathname + new URL(inviteUrl).search;
    await page.goto(invitePath);
    await page.getByRole("button", { name: /accept invitation/i }).click();
    await expect(page).toHaveURL("/", { timeout: 15000 });
  });
});

test.describe("Billing smoke", () => {
  test("pricing page loads plan tiers", async ({ page }) => {
    await page.goto("/pricing");
    await expect(page.getByText("Simple pricing for governed change")).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("SAML SSO")).toBeVisible();
  });
});
