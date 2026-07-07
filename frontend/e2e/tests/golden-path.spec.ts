import { test, expect } from "@playwright/test";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";

test.describe("Golden path", () => {
  test("register, login, and reach dashboard", async ({ page }) => {
    const email = `e2e_${Date.now()}@stackpilot.test`;
    const password = "TestPassword123!";

    await page.goto("/login");
    await page.getByRole("button", { name: /need an account/i }).click();
    await page.getByLabel("First Name").fill("E2E");
    await page.getByLabel("Last Name").fill("User");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: /create account/i }).click();

    await expect(page).toHaveURL("/", { timeout: 15000 });
    await expect(page.getByText("Workspace Dashboard")).toBeVisible();
  });

  test("full workflow: org, ticket, requirements, approval queue", async ({ page, request }) => {
    const email = `golden_${Date.now()}@stackpilot.test`;
    const password = "TestPassword123!";

    await request.post(`${apiBase}/auth/register`, {
      data: { email, password, firstName: "Golden", lastName: "Path" },
    });

    const loginRes = await request.post(`${apiBase}/auth/login`, { data: { email, password } });
    const loginBody = await loginRes.json();
    const token = loginBody.data.accessToken as string;

    const orgRes = await request.post(`${apiBase}/organizations`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: "Golden Path Org", slug: `gp-${Date.now()}` },
    });
    const orgBody = await orgRes.json();
    const orgId = orgBody.data.organization.id as string;
    const orgToken = orgBody.data.accessToken as string;

    const wsRes = await request.get(`${apiBase}/organizations/${orgId}/workspaces`, {
      headers: { Authorization: `Bearer ${orgToken}`, "X-Organization-Id": orgId },
    });
    const wsBody = await wsRes.json();
    const workspaceId = wsBody.data[0].id as string;

    const ticketRes = await request.post(`${apiBase}/workspaces/${workspaceId}/tickets`, {
      headers: {
        Authorization: `Bearer ${orgToken}`,
        "X-Organization-Id": orgId,
        "X-Workspace-Id": workspaceId,
      },
      data: {
        title: "Golden path ticket",
        description: "E2E workflow validation",
        ticketType: "Enhancement",
        priority: "Medium",
        businessJustification: "CI golden path",
      },
    });
    expect(ticketRes.ok()).toBeTruthy();
    const ticketBody = await ticketRes.json();
    const ticketId = ticketBody.data.id as string;

    const reqRes = await request.post(`${apiBase}/tickets/${ticketId}/generate-requirements`, {
      headers: {
        Authorization: `Bearer ${orgToken}`,
        "X-Organization-Id": orgId,
        "X-Workspace-Id": workspaceId,
      },
    });
    expect(reqRes.ok()).toBeTruthy();

    const pendingRes = await request.get(`${apiBase}/workspaces/${workspaceId}/approvals/pending`, {
      headers: {
        Authorization: `Bearer ${orgToken}`,
        "X-Organization-Id": orgId,
        "X-Workspace-Id": workspaceId,
      },
    });
    expect(pendingRes.ok()).toBeTruthy();

    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password").fill(password);
    await page.getByRole("button", { name: /sign in/i }).click();
    await expect(page).toHaveURL("/", { timeout: 15000 });

    await page.goto(`/tickets/${ticketId}`);
    await expect(page.getByText("Golden path ticket")).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole("button", { name: /requirements/i })).toBeVisible();
  });

  test("health endpoint responds", async ({ request }) => {
    const res = await request.get(`${apiBase.replace("/api/v1", "")}/health`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.status).toBe("healthy");
  });
});
