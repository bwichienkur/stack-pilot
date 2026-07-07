"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { StatCard, Card, CardContent, CardHeader, CardTitle, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useDashboard, useAuditLogs } from "@/lib/api-hooks";
import {
  GitBranch, Database, Ticket, AlertTriangle, CheckCircle, Lightbulb, Plug, Activity, Plus,
  ShieldAlert, HeartPulse, FlaskConical, Calendar
} from "lucide-react";

export default function DashboardPage() {
  const { token, orgId, workspaceId, setOrg, setWorkspace } = useAuth();
  const router = useRouter();
  const { data: stats, isLoading: statsLoading, error: statsError } = useDashboard();
  const { data: auditData } = useAuditLogs(1, 5);
  const activity = auditData?.items ?? [];

  useEffect(() => {
    if (!token) {
      router.push("/login");
      return;
    }
    initWorkspace();
  }, [token]);

  const initWorkspace = async () => {
    try {
      let currentOrgId = orgId;
      let currentWsId = workspaceId;

      if (!currentOrgId) {
        const orgs = await api<{ id: string; name: string }[]>("/organizations", {}, token!);
        if (orgs.length === 0) {
          router.replace("/onboarding");
          return;
        }
        currentOrgId = orgs[0].id;
        setOrg(currentOrgId);
      }

      if (!currentWsId && currentOrgId) {
        const workspaces = await api<{ id: string }[]>(
          `/organizations/${currentOrgId}/workspaces`,
          {},
          token!,
          currentOrgId
        );
        if (workspaces.length > 0) {
          currentWsId = workspaces[0].id;
          setWorkspace(currentWsId);
        }
      }
    } catch {
      // hooks surface load errors
    }
  };

  if (!token) return null;

  const loading = statsLoading && !!workspaceId;
  const error = statsError instanceof Error ? statsError.message : "";

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Workspace Dashboard</h1>
            <p className="text-zinc-400 mt-1">Executive overview of risk, quality, and delivery</p>
          </div>
          <div className="flex gap-3">
            <Link href="/connectors"><Button variant="secondary"><Plug className="h-4 w-4" /> Connect</Button></Link>
            <Link href="/tickets/new"><Button><Plus className="h-4 w-4" /> New Ticket</Button></Link>
          </div>
        </div>

        {error && <p className="text-sm text-red-400">{error}</p>}

        <div>
          <h2 className="text-sm font-medium text-zinc-400 uppercase tracking-wide mb-3">Executive Risk</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {loading ? (
              [...Array(4)].map((_, i) => (
                <div key={i} className="h-32 rounded-xl bg-zinc-900 animate-pulse" />
              ))
            ) : (
              <>
                <StatCard
                  title="High Risk Items"
                  value={stats?.highRiskCount ?? 0}
                  subtitle="Risk score ≥ 7"
                  icon={ShieldAlert}
                />
                <StatCard
                  title="Unhealthy Connectors"
                  value={stats?.unhealthyConnectors ?? 0}
                  subtitle="Needs attention"
                  icon={HeartPulse}
                />
                <StatCard
                  title="Pending QA / UAT"
                  value={(stats?.pendingQaCount ?? 0) + (stats?.pendingUatCount ?? 0)}
                  subtitle={`QA ${stats?.pendingQaCount ?? 0} · UAT ${stats?.pendingUatCount ?? 0}`}
                  icon={FlaskConical}
                />
                <StatCard
                  title="Releases This Week"
                  value={stats?.releasesThisWeek ?? 0}
                  subtitle="Scheduled production"
                  icon={Calendar}
                />
              </>
            )}
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
              {activity.length === 0 ? (
                <p className="text-sm text-zinc-500">No activity yet. Connect a repository or create a ticket to get started.</p>
              ) : (
                <div className="space-y-4">
                  {activity.map((entry) => (
                    <div key={entry.id} className="flex items-center gap-3 py-2 border-b border-zinc-800 last:border-0">
                      <div className="h-2 w-2 rounded-full bg-indigo-500" />
                      <span className="text-sm text-zinc-300">{entry.action}</span>
                      <span className="text-xs text-zinc-500 ml-auto">{new Date(entry.createdAt).toLocaleString()}</span>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-amber-400" />
                Getting Started
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm text-zinc-400">
              <p>1. Connect your GitHub or GitLab repository from the Connectors page.</p>
              <p>2. Sync to populate your architecture graph automatically.</p>
              <p>3. Create a ticket to generate AI-powered requirements.</p>
              {!workspaceId && (
                <p className="text-amber-400">Create a workspace in Settings to enable full functionality.</p>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </AppLayout>
  );
}
