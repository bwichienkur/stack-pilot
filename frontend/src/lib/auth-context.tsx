"use client";

import { createContext, useContext, useEffect, useState, useCallback, ReactNode } from "react";
import { API_BASE } from "@/lib/utils";

interface AuthUser {
  id: string;
  email: string;
  firstName?: string;
  lastName?: string;
}

interface AuthState {
  token: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  orgId: string | null;
  workspaceId: string | null;
  featureFlags: Record<string, boolean> | null;
  setAuth: (token: string, user: AuthUser, refreshToken?: string | null) => void;
  setOrg: (orgId: string) => void;
  setWorkspace: (workspaceId: string) => void;
  setFeatureFlags: (flags: Record<string, boolean>) => void;
  refreshSession: () => Promise<boolean>;
  logout: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

function loadStoredAuth() {
  if (typeof window === "undefined") {
    return { token: null, refreshToken: null, user: null, orgId: null, workspaceId: null, featureFlags: null };
  }
  try {
    const stored = localStorage.getItem("stackpilot_auth");
    if (!stored) return { token: null, refreshToken: null, user: null, orgId: null, workspaceId: null, featureFlags: null };
    const parsed = JSON.parse(stored);
    return {
      token: parsed.token ?? null,
      refreshToken: parsed.refreshToken ?? null,
      user: parsed.user ?? null,
      orgId: parsed.orgId ?? null,
      workspaceId: parsed.workspaceId ?? null,
      featureFlags: parsed.featureFlags ?? null,
    };
  } catch {
    return { token: null, refreshToken: null, user: null, orgId: null, workspaceId: null, featureFlags: null };
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const stored = loadStoredAuth();
  const [token, setToken] = useState<string | null>(stored.token);
  const [refreshToken, setRefreshToken] = useState<string | null>(stored.refreshToken);
  const [user, setUser] = useState<AuthUser | null>(stored.user);
  const [orgId, setOrgId] = useState<string | null>(stored.orgId);
  const [workspaceId, setWorkspaceId] = useState<string | null>(stored.workspaceId);
  const [featureFlags, setFeatureFlagsState] = useState<Record<string, boolean> | null>(stored.featureFlags);

  useEffect(() => {
    const fresh = loadStoredAuth();
    setToken(fresh.token);
    setRefreshToken(fresh.refreshToken);
    setUser(fresh.user);
    setOrgId(fresh.orgId);
    setWorkspaceId(fresh.workspaceId);
    setFeatureFlagsState(fresh.featureFlags);
  }, []);

  const persist = (data: {
    token?: string | null;
    refreshToken?: string | null;
    user?: AuthUser | null;
    orgId?: string | null;
    workspaceId?: string | null;
    featureFlags?: Record<string, boolean> | null;
  }) => {
    const current = {
      token: data.token ?? token,
      refreshToken: data.refreshToken ?? refreshToken,
      user: data.user ?? user,
      orgId: data.orgId ?? orgId,
      workspaceId: data.workspaceId ?? workspaceId,
      featureFlags: data.featureFlags ?? featureFlags,
    };
    localStorage.setItem("stackpilot_auth", JSON.stringify(current));
  };

  const setAuth = (t: string, u: AuthUser, rt?: string | null) => {
    setToken(t);
    setUser(u);
    if (rt !== undefined) setRefreshToken(rt);
    persist({ token: t, user: u, refreshToken: rt ?? refreshToken });
    if (typeof document !== "undefined") {
      document.cookie = "stackpilot_auth_hint=1; path=/; max-age=28800; SameSite=Lax";
    }
  };

  const setOrg = (id: string) => {
    setOrgId(id);
    persist({ orgId: id });
  };

  const setWorkspace = (id: string) => {
    setWorkspaceId(id);
    persist({ workspaceId: id });
  };

  const setFeatureFlags = (flags: Record<string, boolean>) => {
    setFeatureFlagsState(flags);
    persist({ featureFlags: flags });
  };

  const refreshSession = useCallback(async (): Promise<boolean> => {
    if (!refreshToken) return false;
    try {
      const res = await fetch(`${API_BASE}/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) return false;
      const json = await res.json();
      const data = json.data;
      if (!data?.accessToken) return false;
      setAuth(data.accessToken, data.user, data.refreshToken);
      return true;
    } catch {
      return false;
    }
  }, [refreshToken, setAuth]);

  useEffect(() => {
    if (!token || !refreshToken) return;
    const interval = setInterval(() => { refreshSession().catch(() => {}); }, 7 * 60 * 60 * 1000);
    return () => clearInterval(interval);
  }, [token, refreshToken, refreshSession]);

  const logout = () => {
    if (refreshToken) {
      fetch(`${API_BASE}/auth/logout`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      }).catch(() => {});
    }
    setToken(null);
    setRefreshToken(null);
    setUser(null);
    setOrgId(null);
    setWorkspaceId(null);
    setFeatureFlagsState(null);
    localStorage.removeItem("stackpilot_auth");
    if (typeof document !== "undefined") {
      document.cookie = "stackpilot_auth_hint=; path=/; max-age=0";
    }
  };

  return (
    <AuthContext.Provider value={{
      token, refreshToken, user, orgId, workspaceId, featureFlags,
      setAuth, setOrg, setWorkspace, setFeatureFlags, refreshSession, logout,
    }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
