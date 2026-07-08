import { expect, type APIRequestContext, type APIResponse, type Page } from "@playwright/test";

const apiBase = process.env.PLAYWRIGHT_API_URL || "http://localhost:5000/api/v1";

export interface AuthSession {
  token: string;
  email: string;
  password: string;
}

export interface OrgSession {
  orgId: string;
  orgToken: string;
}

export function buildUniqueEmail(prefix: string): string {
  return `${prefix}_${Date.now()}_${Math.random().toString(36).slice(2, 8)}@stackpilot.test`;
}

export async function expectOk(response: APIResponse, label: string): Promise<void> {
  if (response.ok()) return;

  const body = await response.text();
  throw new Error(`${label} failed (${response.status()}): ${body}`);
}

export async function registerViaApi(
  request: APIRequestContext,
  args: { email: string; password: string; firstName: string; lastName: string }
): Promise<AuthSession> {
  const registerRes = await request.post(`${apiBase}/auth/register`, { data: args });
  await expectOk(registerRes, "Register API");
  const registerBody = await registerRes.json();
  const token = registerBody.data.accessToken as string;
  expect(token).toBeTruthy();
  return { token, email: args.email, password: args.password };
}

export async function loginViaApi(request: APIRequestContext, email: string, password: string): Promise<string> {
  const loginRes = await request.post(`${apiBase}/auth/login`, { data: { email, password } });
  await expectOk(loginRes, "Login API");
  const loginBody = await loginRes.json();
  const token = loginBody.data.accessToken as string;
  expect(token).toBeTruthy();
  return token;
}

export async function createOrganization(
  request: APIRequestContext,
  token: string,
  args: { name: string; slug: string }
): Promise<OrgSession> {
  const orgRes = await request.post(`${apiBase}/organizations`, {
    headers: { Authorization: `Bearer ${token}` },
    data: args,
  });
  await expectOk(orgRes, "Create organization API");
  const orgBody = await orgRes.json();
  return {
    orgId: orgBody.data.organization.id as string,
    orgToken: orgBody.data.accessToken as string,
  };
}

export function orgHeaders(token: string, orgId: string, workspaceId?: string): Record<string, string> {
  return {
    Authorization: `Bearer ${token}`,
    "X-Organization-Id": orgId,
    ...(workspaceId ? { "X-Workspace-Id": workspaceId } : {}),
  };
}

export async function signInViaUi(page: Page, email: string, password: string): Promise<void> {
  await page.goto("/login");
  await expect(page.getByLabel("Email")).toBeVisible({ timeout: 15000 });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: /sign in/i }).click();
}

export async function registerViaUi(
  page: Page,
  args: { firstName: string; lastName: string; email: string; password: string }
): Promise<void> {
  await page.goto("/login");
  await expect(page.getByLabel("Email")).toBeVisible({ timeout: 15000 });
  await page.getByRole("button", { name: /need an account/i }).click();
  await page.getByLabel("First Name").fill(args.firstName);
  await page.getByLabel("Last Name").fill(args.lastName);
  await page.getByLabel("Email").fill(args.email);
  await page.getByLabel("Password").fill(args.password);
  await page.getByRole("button", { name: /create account/i }).click();
}
