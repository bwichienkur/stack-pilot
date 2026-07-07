import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export const API_BASE = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api/v1";

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

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  const json = await res.json();
  if (!res.ok) throw new Error(json.errors?.[0]?.message || "API error");
  return json.data;
}
