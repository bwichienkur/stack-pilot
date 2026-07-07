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

  test("health endpoint responds", async ({ request }) => {
    const res = await request.get(`${apiBase.replace("/api/v1", "")}/health`);
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.status).toBe("healthy");
  });
});
