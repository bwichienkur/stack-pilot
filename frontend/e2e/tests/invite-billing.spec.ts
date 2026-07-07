import { test, expect } from "@playwright/test";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";

test.describe("Invite accept flow", () => {
  test("create invite, register invitee, accept via UI", async ({ page, request }) => {
    const inviterEmail = `inviter_${Date.now()}@stackpilot.test`;
    const inviteeEmail = `invitee_${Date.now()}@stackpilot.test`;
    const password = "TestPassword123!";

    await request.post(`${apiBase}/auth/register`, {
      data: { email: inviterEmail, password, firstName: "Inviter", lastName: "User" },
    });
    const loginRes = await request.post(`${apiBase}/auth/login`, { data: { email: inviterEmail, password } });
    const token = (await loginRes.json()).data.accessToken as string;

    const orgRes = await request.post(`${apiBase}/organizations`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: "Invite Org", slug: `inv-${Date.now()}` },
    });
    const orgBody = await orgRes.json();
    const orgId = orgBody.data.organization.id as string;
    const orgToken = orgBody.data.accessToken as string;

    const rolesRes = await request.get(`${apiBase}/organizations/roles`, {
      headers: { Authorization: `Bearer ${orgToken}`, "X-Organization-Id": orgId },
    });
    const rolesBody = await rolesRes.json();
    const developerRole = rolesBody.data.find((r: { name: string }) => r.name === "Developer");

    const inviteRes = await request.post(`${apiBase}/organizations/${orgId}/invites`, {
      headers: { Authorization: `Bearer ${orgToken}`, "X-Organization-Id": orgId },
      data: { email: inviteeEmail, roleId: developerRole.id },
    });
    const inviteBody = await inviteRes.json();
    const inviteUrl = inviteBody.data.inviteUrl as string;

    await page.goto("/login");
    await page.getByRole("button", { name: /need an account/i }).click();
    await page.getByLabel("First Name").fill("Invited");
    await page.getByLabel("Last Name").fill("User");
    await page.getByLabel("Email").fill(inviteeEmail);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: /create account/i }).click();
    await expect(page).toHaveURL("/", { timeout: 15000 });

    await page.goto(inviteUrl.replace("http://localhost:3000", ""));
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
