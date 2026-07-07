import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api/v1";

async function tryRefreshToken(): Promise<string | null> {
  if (typeof window === "undefined") return null;
  try {
    const stored = localStorage.getItem("stackpilot_auth");
    if (!stored) return null;
    const { refreshToken } = JSON.parse(stored);
    if (!refreshToken) return null;

    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return null;

    const json = await res.json();
    const data = json.data;
    if (!data?.accessToken) return null;

    const current = JSON.parse(stored);
    const updated = {
      ...current,
      token: data.accessToken,
      refreshToken: data.refreshToken,
      user: data.user,
    };
    localStorage.setItem("stackpilot_auth", JSON.stringify(updated));
    return data.accessToken as string;
  } catch {
    return null;
  }
}

function clearAuthAndRedirect() {
  if (typeof window === "undefined") return;
  localStorage.removeItem("stackpilot_auth");
  document.cookie = "stackpilot_auth_hint=; path=/; max-age=0";
  window.location.href = "/login";
}

export async function api<T>(
  path: string,
  options: RequestInit = {},
  token?: string | null,
  orgId?: string | null,
  workspaceId?: string | null
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  if (orgId) headers["X-Organization-Id"] = orgId;
  if (workspaceId) headers["X-Workspace-Id"] = workspaceId;

  let res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (res.status === 401 && typeof window !== "undefined") {
    const newToken = await tryRefreshToken();
    if (newToken) {
      headers["Authorization"] = `Bearer ${newToken}`;
      res = await fetch(`${API_BASE}${path}`, { ...options, headers });
    }
  }

  const json = await res.json().catch(() => ({}));
  if (res.status === 401) {
    clearAuthAndRedirect();
    throw new Error("Session expired");
  }
  if (!res.ok) throw new Error(json.errors?.[0]?.message || "API error");
  return json.data;
}
