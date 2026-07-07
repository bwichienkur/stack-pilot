"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, GitBranch, Network, FileText, Lightbulb,
  Ticket, CheckCircle, FlaskConical, Rocket, ScrollText, Settings,
  Plug, Bot, ChevronLeft, ChevronRight, LogOut, Calendar, Menu, X, Shield
} from "lucide-react";
import { cn, api } from "@/lib/utils";
import { useAuth } from "@/lib/auth-context";
import { isNavEnabled } from "@/lib/feature-flags";
import { useIsSuperAdmin } from "@/lib/api-hooks";
import { CommandPalette } from "@/components/command-palette";
import { AiCopilotPanel } from "@/components/ai-copilot-panel";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/applications", label: "Applications", icon: GitBranch },
  { href: "/connectors", label: "Connectors", icon: Plug },
  { href: "/architecture", label: "Architecture", icon: Network },
  { href: "/docs", label: "Documentation", icon: FileText },
  { href: "/recommendations", label: "Recommendations", icon: Lightbulb },
  { href: "/tickets", label: "Tickets", icon: Ticket },
  { href: "/approvals", label: "Approvals", icon: CheckCircle },
  { href: "/qa", label: "QA Queue", icon: FlaskConical },
  { href: "/uat", label: "UAT Queue", icon: CheckCircle },
  { href: "/deployments", label: "Deployments", icon: Rocket },
  { href: "/releases", label: "Releases", icon: Calendar },
  { href: "/audit", label: "Audit Logs", icon: ScrollText },
  { href: "/settings", label: "Settings", icon: Settings },
];

function SidebarNav({
  collapsed,
  visibleNav,
  pathname,
  onNavigate,
}: {
  collapsed: boolean;
  visibleNav: typeof navItems;
  pathname: string;
  onNavigate?: () => void;
}) {
  return (
    <nav className="flex-1 overflow-y-auto py-4 px-2 space-y-1">
      {visibleNav.map((item) => {
        const isActive = pathname === item.href || (item.href !== "/" && pathname.startsWith(item.href));
        return (
          <Link
            key={item.href}
            href={item.href}
            onClick={onNavigate}
            className={cn(
              "flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-all",
              isActive
                ? "bg-indigo-500/10 text-indigo-400 border border-indigo-500/20"
                : "text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50"
            )}
          >
            <item.icon className="h-4 w-4 flex-shrink-0" />
            {!collapsed && <span>{item.label}</span>}
          </Link>
        );
      })}
    </nav>
  );
}

export function Sidebar({ mobileOpen, onMobileClose }: { mobileOpen?: boolean; onMobileClose?: () => void }) {
  const pathname = usePathname();
  const { user, logout, featureFlags } = useAuth();
  const { data: isSuperAdmin } = useIsSuperAdmin();
  const [collapsed, setCollapsed] = useState(false);

  const adminNav = isSuperAdmin ? [{ href: "/admin", label: "Platform Admin", icon: Shield }] : [];
  const visibleNav = [...navItems, ...adminNav].filter((item) => isNavEnabled(item.href, featureFlags));

  const sidebarContent = (
    <>
      <div className="flex items-center gap-3 px-4 h-16 border-b border-zinc-800">
        <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center flex-shrink-0">
          <Bot className="h-5 w-5 text-white" />
        </div>
        {!collapsed && (
          <div className="flex-1 min-w-0">
            <h1 className="text-lg font-bold text-zinc-100">StackPilot</h1>
            <p className="text-[10px] text-zinc-500 uppercase tracking-wider">Enterprise Intelligence</p>
          </div>
        )}
        {onMobileClose && (
          <button
            type="button"
            onClick={onMobileClose}
            className="lg:hidden p-2 text-zinc-400 hover:text-zinc-200"
            aria-label="Close menu"
          >
            <X className="h-5 w-5" />
          </button>
        )}
      </div>

      <SidebarNav
        collapsed={collapsed}
        visibleNav={visibleNav}
        pathname={pathname}
        onNavigate={onMobileClose}
      />

      <div className="border-t border-zinc-800 p-3 space-y-2">
        {!collapsed && user && (
          <div className="px-3 py-2">
            <p className="text-sm font-medium text-zinc-200">{user.firstName} {user.lastName}</p>
            <p className="text-xs text-zinc-500">{user.email}</p>
          </div>
        )}
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="hidden lg:flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 w-full transition-all"
        >
          {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
          {!collapsed && <span>Collapse</span>}
        </button>
        <button
          onClick={logout}
          className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-zinc-400 hover:text-red-400 hover:bg-red-500/10 w-full transition-all"
        >
          <LogOut className="h-4 w-4" />
          {!collapsed && <span>Sign out</span>}
        </button>
      </div>
    </>
  );

  if (mobileOpen !== undefined) {
    return (
      <aside
        className={cn(
          "fixed inset-y-0 left-0 z-40 flex flex-col w-64 border-r border-zinc-800 bg-zinc-950 transition-transform duration-300 lg:hidden",
          mobileOpen ? "translate-x-0" : "-translate-x-full"
        )}
      >
        {sidebarContent}
      </aside>
    );
  }

  return (
    <aside className={cn(
      "hidden lg:flex flex-col h-screen border-r border-zinc-800 bg-zinc-950 transition-all duration-300",
      collapsed ? "w-16" : "w-64"
    )}>
      {sidebarContent}
    </aside>
  );
}

export function OrgWorkspaceSwitcher() {
  const { token, orgId, workspaceId, setOrg, setWorkspace, setFeatureFlags } = useAuth();
  const [orgs, setOrgs] = useState<{ id: string; name: string }[]>([]);
  const [workspaces, setWorkspaces] = useState<{ id: string; name: string }[]>([]);

  useEffect(() => {
    if (!token) return;
    api<{ id: string; name: string }[]>("/organizations", {}, token)
      .then(setOrgs)
      .catch(() => {});
  }, [token]);

  useEffect(() => {
    if (!token || !orgId) return;
    api<{ id: string; name: string }[]>(`/organizations/${orgId}/workspaces`, {}, token, orgId)
      .then((ws) => {
        setWorkspaces(ws);
        if (!workspaceId && ws.length > 0) setWorkspace(ws[0].id);
      })
      .catch(() => {});

    api<{ featureFlags: Record<string, boolean> }>(`/organizations/${orgId}/settings`, {}, token, orgId)
      .then((s) => setFeatureFlags(s.featureFlags || {}))
      .catch(() => setFeatureFlags({}));
  }, [token, orgId, workspaceId, setWorkspace, setFeatureFlags]);

  if (!token || orgs.length === 0) return null;

  return (
    <div className="flex items-center gap-2 flex-wrap">
      <select
        value={orgId || ""}
        onChange={(e) => setOrg(e.target.value)}
        className="h-8 rounded-md border border-zinc-700 bg-zinc-900 px-2 text-xs text-zinc-300 max-w-[140px] sm:max-w-none"
      >
        {orgs.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
      </select>
      {workspaces.length > 0 && (
        <select
          value={workspaceId || ""}
          onChange={(e) => setWorkspace(e.target.value)}
          className="h-8 rounded-md border border-zinc-700 bg-zinc-900 px-2 text-xs text-zinc-300 max-w-[140px] sm:max-w-none"
        >
          {workspaces.map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}
        </select>
      )}
    </div>
  );
}

export function TopBar({ onMenuClick }: { onMenuClick?: () => void }) {
  return (
    <header className="h-16 border-b border-zinc-800 bg-zinc-950/80 backdrop-blur-sm flex items-center justify-between px-4 lg:px-6 gap-4">
      <div className="flex items-center gap-3 min-w-0">
        {onMenuClick && (
          <button
            type="button"
            onClick={onMenuClick}
            className="lg:hidden p-2 -ml-2 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50"
            aria-label="Open menu"
          >
            <Menu className="h-5 w-5" />
          </button>
        )}
        <OrgWorkspaceSwitcher />
      </div>
      <div className="flex items-center gap-3 flex-shrink-0">
        <div className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
        <span className="text-xs text-zinc-400 hidden sm:inline">All systems operational</span>
      </div>
    </header>
  );
}

export function AppLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <div className="flex h-screen bg-zinc-950">
      <Sidebar />
      <Sidebar mobileOpen={mobileOpen} onMobileClose={() => setMobileOpen(false)} />
      {mobileOpen && (
        <button
          type="button"
          className="fixed inset-0 z-30 bg-black/60 lg:hidden"
          onClick={() => setMobileOpen(false)}
          aria-label="Close overlay"
        />
      )}
      <div className="flex-1 flex flex-col overflow-hidden min-w-0">
        <TopBar onMenuClick={() => setMobileOpen(true)} />
        <main className="flex-1 overflow-y-auto p-4 lg:p-6">{children}</main>
      </div>
      <CommandPalette />
      <AiCopilotPanel />
    </div>
  );
}
