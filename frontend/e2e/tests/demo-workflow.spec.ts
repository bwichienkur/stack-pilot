import { test, expect } from "@playwright/test";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";
const demoEmail = "demo@stackpilot.dev";
const demoPassword = "DemoPassword123!";

test.describe("Demo workflow", () => {
  test.describe.configure({ retries: 0 });
  test.skip(!process.env.DEMO_SEED, "Requires DEMO_SEED=true on the API");

  test("login, approve, QA, UAT, and release calendar", async ({ page, request }) => {
    await page.goto("/login");
    await expect(page.getByLabel("Email")).toBeVisible({ timeout: 15000 });
    await page.getByLabel("Email").fill(demoEmail);
    await page.getByLabel("Password").fill(demoPassword);
    await page.getByRole("button", { name: /sign in/i }).click();

    await expect(page).toHaveURL("/", { timeout: 15000 });
    await expect(page.getByText("Workspace Dashboard")).toBeVisible();
    await page.getByRole("link", { name: "Approvals" }).click();
    await expect(page.getByRole("heading", { name: "Approval Queue" })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Add two-factor authentication")).toBeVisible();
    await page.getByRole("button", { name: /approve/i }).first().click();
    await expect(page.getByText(/ticket approved/i)).toBeVisible({ timeout: 10000 });

    const loginRes = await request.post(`${apiBase}/auth/login`, {
      data: { email: demoEmail, password: demoPassword },
    });
    const loginBody = await loginRes.json();
    const token = loginBody.data.accessToken as string;

    const orgsRes = await request.get(`${apiBase}/organizations`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    const orgsBody = await orgsRes.json();
    const orgId = orgsBody.data[0].id as string;

    const wsRes = await request.get(`${apiBase}/organizations/${orgId}/workspaces`, {
      headers: { Authorization: `Bearer ${token}`, "X-Organization-Id": orgId },
    });
    const wsBody = await wsRes.json();
    const workspaceId = wsBody.data[0].id as string;

    const ticketsRes = await request.get(`${apiBase}/workspaces/${workspaceId}/tickets`, {
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Organization-Id": orgId,
        "X-Workspace-Id": workspaceId,
      },
    });
    const ticketsBody = await ticketsRes.json();
    const tickets = ticketsBody.data.items as { id: string; title: string }[];
    const demoTicket = tickets.find((t) => t.title.includes("two-factor authentication"));
    expect(demoTicket).toBeTruthy();
    const ticketId = demoTicket!.id as string;

    const patchStatuses = ["ImplementationInProgress", "BuildRunning", "DeployedToTest"] as const;
    for (const status of patchStatuses) {
      const patchRes = await request.patch(`${apiBase}/tickets/${ticketId}`, {
        headers: {
          Authorization: `Bearer ${token}`,
          "X-Organization-Id": orgId,
          "X-Workspace-Id": workspaceId,
        },
        data: { status },
      });
      expect(patchRes.ok()).toBeTruthy();
    }

    await page.getByRole("link", { name: "QA Queue" }).click();
    await expect(page.getByRole("heading", { name: "QA Queue" })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Add two-factor authentication")).toBeVisible();
    await page.getByRole("button", { name: /pass/i }).first().click();
    await expect(page.getByText(/qa passed/i)).toBeVisible({ timeout: 10000 });

    await page.getByRole("link", { name: "UAT Queue" }).click();
    await expect(page.getByRole("heading", { name: "UAT Queue" })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText("Add two-factor authentication")).toBeVisible();
    await page.getByRole("button", { name: /accept/i }).first().click();
    await expect(page.getByText(/uat accepted/i)).toBeVisible({ timeout: 10000 });

    await page.getByRole("link", { name: "Releases" }).click();
    await expect(page.getByRole("heading", { name: "Release Calendar" })).toBeVisible({ timeout: 10000 });
    await expect(page.locator("main").getByText("Add two-factor authentication")).toBeVisible();
  });
});
