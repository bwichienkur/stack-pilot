import { test, expect } from "@playwright/test";
import {
  buildUniqueEmail,
  createOrganization,
  expectOk,
  loginViaApi,
  orgHeaders,
  registerViaApi,
  signInViaUi,
  registerViaUi,
} from "./helpers";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";

test.describe("Golden path", () => {
  test("register, login, and reach dashboard", async ({ page }) => {
    const email = buildUniqueEmail("e2e");
    const password = "TestPassword123!";

    await registerViaUi(page, { firstName: "E2E", lastName: "User", email, password });

    await expect(page).toHaveURL("/onboarding", { timeout: 15000 });
  });

  test("full workflow: org, ticket, requirements, approval queue", async ({ page, request }) => {
    const email = buildUniqueEmail("golden");
    const password = "TestPassword123!";

    const auth = await registerViaApi(request, { email, password, firstName: "Golden", lastName: "Path" });
    const token = await loginViaApi(request, email, password);
    const { orgId, orgToken } = await createOrganization(request, token, {
      name: "Golden Path Org",
      slug: `gp-${Date.now()}`,
    });

    const wsRes = await request.get(`${apiBase}/organizations/${orgId}/workspaces`, {
      headers: orgHeaders(orgToken, orgId),
    });
    await expectOk(wsRes, "List workspaces API");
    const wsBody = await wsRes.json();
    const workspaceId = wsBody.data[0].id as string;

    const ticketRes = await request.post(`${apiBase}/workspaces/${workspaceId}/tickets`, {
      headers: orgHeaders(orgToken, orgId, workspaceId),
      data: {
        title: "Golden path ticket",
        description: "E2E workflow validation",
        ticketType: "Enhancement",
        priority: "Medium",
        businessJustification: "CI golden path",
      },
    });
    await expectOk(ticketRes, "Create ticket API");
    const ticketBody = await ticketRes.json();
    const ticketId = ticketBody.data.id as string;

    const reqRes = await request.post(`${apiBase}/tickets/${ticketId}/generate-requirements`, {
      headers: orgHeaders(orgToken, orgId, workspaceId),
    });
    await expectOk(reqRes, "Generate requirements API");

    const pendingRes = await request.get(`${apiBase}/workspaces/${workspaceId}/approvals/pending`, {
      headers: orgHeaders(orgToken, orgId, workspaceId),
    });
    await expectOk(pendingRes, "Pending approvals API");

    await signInViaUi(page, auth.email, auth.password);
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
