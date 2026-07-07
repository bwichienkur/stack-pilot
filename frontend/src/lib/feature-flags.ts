export const STUB_NAV_FLAGS = [
  "applications",
  "docs",
  "recommendations",
  "qa",
  "uat",
  "deployments",
] as const;

export type StubNavFlag = (typeof STUB_NAV_FLAGS)[number];

export const NAV_FLAG_BY_PATH: Record<string, StubNavFlag> = {
  "/applications": "applications",
  "/docs": "docs",
  "/recommendations": "recommendations",
  "/qa": "qa",
  "/uat": "uat",
  "/deployments": "deployments",
};

export function isNavEnabled(path: string, orgFlags?: Record<string, boolean> | null): boolean {
  const flag = NAV_FLAG_BY_PATH[path];
  if (!flag) return true;
  return orgFlags?.[flag] === true;
}
