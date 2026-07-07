"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { StatCard, Card, CardContent, CardHeader, CardTitle, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import {
  GitBranch, Database, Ticket, AlertTriangle, CheckCircle, Lightbulb, Plug, Activity, Plus
} from "lucide-react";
import Link from "next/link";

interface DashboardStats {
  applicationCount: number;
  repositoryCount: number;
  databaseCount: number;
  openTickets: number;
  pendingApprovals: number;
  openRecommendations: number;
  averageRiskScore: number;
  activeConnectors: number;
}

export default function DashboardPage() {
  const { token, orgId, workspaceId, setOrg, setWorkspace } = useAuth();
  const router = useRouter();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    initWorkspace();
  }, [token]);

  const initWorkspace = async () => {
    try {
      let currentOrgId = orgId;
      let currentWsId = workspaceId;

      if (!currentOrgId) {
        const orgs = await api<{ id: string; name: string }[]>("/organizations", {}, token);
        if (orgs.length === 0) {
          const org = await api<{ id: string }>("/organizations", {
            method: "POST", body: JSON.stringify({ name: "My Organization", slug: "my-org" })
          }, token);
          currentOrgId = org.id;
        } else {
          currentOrgId = orgs[0].id;
        }
        setOrg(currentOrgId!);
      }

      if (!currentWsId) {
        const workspaces = await api<{ id: string }[]>(`/organizations/${currentOrgId}/workspaces`, {}, token, currentOrgId);
        if (workspaces.length > 0) {
          currentWsId = workspaces[0].id;
          setWorkspace(currentWsId);
        }
      }

      if (currentWsId) {
        const data = await api<DashboardStats>(`/workspaces/${currentWsId}/dashboard`, {}, token, currentOrgId, currentWsId);
        setStats(data);
      }
    } catch (err) {
      console.error("Dashboard init error:", err);
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Workspace Dashboard</h1>
            <p className="text-zinc-400 mt-1">Overview of your software ecosystem</p>
          </div>
          <div className="flex gap-3">
            <Link href="/connectors"><Button variant="secondary"><Plug className="h-4 w-4" /> Connect</Button></Link>
            <Link href="/tickets"><Button><Plus className="h-4 w-4" /> New Ticket</Button></Link>
          </div>
        </div>

        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {[...Array(8)].map((_, i) => (
              <div key={i} className="h-32 rounded-xl bg-zinc-900 animate-pulse" />
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard title="Applications" value={stats?.applicationCount ?? 0} icon={GitBranch} />
            <StatCard title="Repositories" value={stats?.repositoryCount ?? 0} icon={GitBranch} />
            <StatCard title="Databases" value={stats?.databaseCount ?? 0} icon={Database} />
            <StatCard title="Open Tickets" value={stats?.openTickets ?? 0} icon={Ticket} />
            <StatCard title="Pending Approvals" value={stats?.pendingApprovals ?? 0} icon={CheckCircle} />
            <StatCard title="Recommendations" value={stats?.openRecommendations ?? 0} icon={Lightbulb} />
            <StatCard title="Risk Score" value={stats?.averageRiskScore?.toFixed(1) ?? "0.0"} subtitle="Average" icon={AlertTriangle} />
            <StatCard title="Active Connectors" value={stats?.activeConnectors ?? 0} icon={Plug} />
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Activity className="h-5 w-5 text-indigo-400" />
                Recent Activity
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {["Repository scan completed", "New ticket submitted", "Connector synced", "AI requirements generated"].map((activity, i) => (
                  <div key={i} className="flex items-center gap-3 py-2 border-b border-zinc-800 last:border-0">
                    <div className="h-2 w-2 rounded-full bg-indigo-500" />
                    <span className="text-sm text-zinc-300">{activity}</span>
                    <span className="text-xs text-zinc-500 ml-auto">{i + 1}h ago</span>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-amber-400" />
                Architecture Health
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {[
                  { label: "Security", score: 85, color: "bg-emerald-500" },
                  { label: "Test Coverage", score: 62, color: "bg-amber-500" },
                  { label: "Documentation", score: 78, color: "bg-indigo-500" },
                  { label: "Dependencies", score: 71, color: "bg-amber-500" },
                ].map((item) => (
                  <div key={item.label}>
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-zinc-400">{item.label}</span>
                      <span className="text-zinc-300">{item.score}%</span>
                    </div>
                    <div className="h-2 rounded-full bg-zinc-800">
                      <div className={`h-2 rounded-full ${item.color} transition-all`} style={{ width: `${item.score}%` }} />
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
