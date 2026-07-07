"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard, GitBranch, Database, Network, FileText, Lightbulb,
  Ticket, CheckCircle, FlaskConical, Rocket, ScrollText, Settings,
  Plug, Bot, ChevronLeft, ChevronRight, LogOut
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuth } from "@/lib/auth-context";
import { useState } from "react";

const navItems = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/applications", label: "Applications", icon: GitBranch },
  { href: "/connectors", label: "Connectors", icon: Plug },
  { href: "/architecture", label: "Architecture", icon: Network },
  { href: "/graph", label: "Knowledge Graph", icon: Database },
  { href: "/docs", label: "Documentation", icon: FileText },
  { href: "/recommendations", label: "Recommendations", icon: Lightbulb },
  { href: "/tickets", label: "Tickets", icon: Ticket },
  { href: "/approvals", label: "Approvals", icon: CheckCircle },
  { href: "/qa", label: "QA Queue", icon: FlaskConical },
  { href: "/uat", label: "UAT Queue", icon: CheckCircle },
  { href: "/deployments", label: "Deployments", icon: Rocket },
  { href: "/audit", label: "Audit Logs", icon: ScrollText },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function Sidebar() {
  const pathname = usePathname();
  const { user, logout } = useAuth();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <aside className={cn(
      "flex flex-col h-screen border-r border-zinc-800 bg-zinc-950 transition-all duration-300",
      collapsed ? "w-16" : "w-64"
    )}>
      <div className="flex items-center gap-3 px-4 h-16 border-b border-zinc-800">
        <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center flex-shrink-0">
          <Bot className="h-5 w-5 text-white" />
        </div>
        {!collapsed && (
          <div>
            <h1 className="text-lg font-bold text-zinc-100">StackPilot</h1>
            <p className="text-[10px] text-zinc-500 uppercase tracking-wider">Enterprise Intelligence</p>
          </div>
        )}
      </div>

      <nav className="flex-1 overflow-y-auto py-4 px-2 space-y-1">
        {navItems.map((item) => {
          const isActive = pathname === item.href || (item.href !== "/" && pathname.startsWith(item.href));
          return (
            <Link
              key={item.href}
              href={item.href}
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

      <div className="border-t border-zinc-800 p-3 space-y-2">
        {!collapsed && user && (
          <div className="px-3 py-2">
            <p className="text-sm font-medium text-zinc-200">{user.firstName} {user.lastName}</p>
            <p className="text-xs text-zinc-500">{user.email}</p>
          </div>
        )}
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="flex items-center gap-3 px-3 py-2 rounded-lg text-sm text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800/50 w-full transition-all"
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
    </aside>
  );
}

export function TopBar() {
  return (
    <header className="h-16 border-b border-zinc-800 bg-zinc-950/80 backdrop-blur-sm flex items-center justify-between px-6">
      <div className="text-sm text-zinc-500">StackPilot Workspace</div>
      <div className="flex items-center gap-3">
        <div className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
        <span className="text-xs text-zinc-400">All systems operational</span>
      </div>
    </header>
  );
}

export function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen bg-zinc-950">
      <Sidebar />
      <div className="flex-1 flex flex-col overflow-hidden">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
