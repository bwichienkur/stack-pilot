"use client";

import { createContext, useContext, useEffect, useState, ReactNode } from "react";

interface AuthState {
  token: string | null;
  user: { id: string; email: string; firstName?: string; lastName?: string } | null;
  orgId: string | null;
  workspaceId: string | null;
  setAuth: (token: string, user: AuthState["user"]) => void;
  setOrg: (orgId: string) => void;
  setWorkspace: (workspaceId: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthState["user"]>(null);
  const [orgId, setOrgId] = useState<string | null>(null);
  const [workspaceId, setWorkspaceId] = useState<string | null>(null);

  useEffect(() => {
    const stored = localStorage.getItem("stackpilot_auth");
    if (stored) {
      const parsed = JSON.parse(stored);
      setToken(parsed.token);
      setUser(parsed.user);
      setOrgId(parsed.orgId);
      setWorkspaceId(parsed.workspaceId);
    }
  }, []);

  const persist = (data: Partial<AuthState>) => {
    const current = {
      token: data.token ?? token,
      user: data.user ?? user,
      orgId: data.orgId ?? orgId,
      workspaceId: data.workspaceId ?? workspaceId,
    };
    localStorage.setItem("stackpilot_auth", JSON.stringify(current));
  };

  const setAuth = (t: string, u: AuthState["user"]) => {
    setToken(t);
    setUser(u);
    persist({ token: t, user: u });
  };

  const setOrg = (id: string) => {
    setOrgId(id);
    persist({ orgId: id });
  };

  const setWorkspace = (id: string) => {
    setWorkspaceId(id);
    persist({ workspaceId: id });
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    setOrgId(null);
    setWorkspaceId(null);
    localStorage.removeItem("stackpilot_auth");
  };

  return (
    <AuthContext.Provider value={{ token, user, orgId, workspaceId, setAuth, setOrg, setWorkspace, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
