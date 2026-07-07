import { chromium } from "@playwright/test";
import fs from "fs";
import path from "path";

const OUT = process.env.SCREENSHOT_DIR || "/opt/cursor/artifacts/screenshots/ui-tour";
const BASE = process.env.PLAYWRIGHT_BASE_URL || "http://localhost:3000";
const API = process.env.API_URL || "http://localhost:5000/api/v1";

fs.mkdirSync(OUT, { recursive: true });

async function shot(page, name, url, opts = {}) {
  await page.goto(url, { waitUntil: "domcontentloaded", timeout: 60000 });
  await page.waitForTimeout(opts.wait ?? 2000);
  const file = path.join(OUT, `${name}.png`);
  await page.screenshot({ path: file, fullPage: opts.fullPage ?? true });
  console.log(`Captured: ${file}`);
  return file;
}

async function login(page) {
  await page.goto(`${BASE}/login`);
  await page.getByLabel("Email").fill("demo@stackpilot.dev");
  await page.getByLabel("Password").fill("DemoPassword123!");
  await page.getByRole("button", { name: /sign in/i }).click();
  await page.waitForTimeout(4000);
}

async function getDemoContext(request) {
  const loginRes = await request.post(`${API}/auth/login`, {
    data: { email: "demo@stackpilot.dev", password: "DemoPassword123!" },
  });
  const loginJson = await loginRes.json();
  const token = loginJson.data.accessToken;
  const orgsRes = await request.get(`${API}/organizations`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  const orgs = (await orgsRes.json()).data;
  const orgId = orgs[0].id;
  const wsRes = await request.get(`${API}/organizations/${orgId}/workspaces`, {
    headers: { Authorization: `Bearer ${token}`, "X-Organization-Id": orgId },
  });
  const wsId = (await wsRes.json()).data[0].id;
  const ticketsRes = await request.get(`${API}/workspaces/${wsId}/tickets?page=1&pageSize=5`, {
    headers: { Authorization: `Bearer ${token}`, "X-Organization-Id": orgId, "X-Workspace-Id": wsId },
  });
  const ticketId = (await ticketsRes.json()).data.items[0]?.id;
  const docsRes = await request.get(`${API}/workspaces/${wsId}/docs?page=1&pageSize=5`, {
    headers: { Authorization: `Bearer ${token}`, "X-Organization-Id": orgId, "X-Workspace-Id": wsId },
  });
  const docId = (await docsRes.json()).data.items[0]?.id;
  return { ticketId, docId };
}

const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
const page = await context.newPage();
const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true });
const mobilePage = await mobile.newPage();

// Public screens
await shot(page, "01-login", `${BASE}/login`);
await shot(page, "02-pricing", `${BASE}/pricing`, { fullPage: true });

await login(page);
const { ticketId, docId } = await getDemoContext(page.request);

const appScreens = [
  ["03-dashboard", "/"],
  ["04-applications", "/applications"],
  ["05-connectors", "/connectors"],
  ["06-architecture", "/architecture"],
  ["07-docs", "/docs"],
  ["08-recommendations", "/recommendations"],
  ["09-tickets-board", "/tickets"],
  ["10-tickets-new", "/tickets/new"],
  ["11-approvals", "/approvals"],
  ["12-qa-queue", "/qa"],
  ["13-uat-queue", "/uat"],
  ["14-deployments", "/deployments"],
  ["15-releases", "/releases"],
  ["16-audit-logs", "/audit"],
  ["17-settings", "/settings"],
  ["18-onboarding", "/onboarding"],
];

for (const [name, route] of appScreens) {
  await shot(page, name, `${BASE}${route}`);
}

if (ticketId) {
  await shot(page, "19-ticket-detail", `${BASE}/tickets/${ticketId}`, { wait: 2500 });
}
if (docId) {
  await shot(page, "20-doc-viewer", `${BASE}/docs/${docId}`, { wait: 2000 });
}

// Mobile dashboard
await mobilePage.goto(`${BASE}/login`);
await mobilePage.getByLabel("Email").fill("demo@stackpilot.dev");
await mobilePage.getByLabel("Password").fill("DemoPassword123!");
await mobilePage.getByRole("button", { name: /sign in/i }).click();
await mobilePage.waitForTimeout(3000);
await shot(mobilePage, "21-mobile-dashboard", `${BASE}/`, { fullPage: false });

// Open command palette on dashboard
await page.goto(`${BASE}/`);
await page.waitForTimeout(1000);
await page.keyboard.press("Meta+k");
await page.waitForTimeout(800);
await page.screenshot({ path: path.join(OUT, "22-command-palette.png") });
console.log(`Captured: ${path.join(OUT, "22-command-palette.png")}`);

// AI copilot panel
await page.goto(`${BASE}/`);
await page.getByRole("button", { name: /open ai copilot/i }).click();
await page.waitForTimeout(800);
await page.screenshot({ path: path.join(OUT, "23-ai-copilot.png") });
console.log(`Captured: ${path.join(OUT, "23-ai-copilot.png")}`);

await browser.close();
console.log(`\nDone — ${OUT}`);
