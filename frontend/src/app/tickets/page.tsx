"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { Plus } from "lucide-react";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";

interface Ticket {
  id: string;
  ticketNumber: number;
  title: string;
  ticketType: string;
  status: string;
  priority: string;
  riskScore?: number;
  createdAt: string;
}

const statusColors: Record<string, "default" | "success" | "warning" | "danger" | "neutral"> = {
  Submitted: "neutral", AiAnalysisPending: "default", RequirementsDrafted: "default",
  AwaitingApproval: "warning", Approved: "success", ImplementationInProgress: "default",
  PullRequestCreated: "default", BuildRunning: "default", DeployedToTest: "default",
  QaInProgress: "default", QaPassed: "success", QaFailed: "danger",
  UatInProgress: "default", UatRejected: "danger", UatAccepted: "success",
  ScheduledForProduction: "warning", DeployedToProduction: "success", Closed: "neutral",
};

const columns: { key: string; label: string; statuses: string[] }[] = [
  { key: "intake", label: "Intake", statuses: ["Submitted", "AiAnalysisPending", "RequirementsDrafted"] },
  { key: "approval", label: "Approval", statuses: ["AwaitingApproval", "Approved"] },
  { key: "build", label: "Implementation", statuses: ["ImplementationInProgress", "PullRequestCreated", "BuildRunning", "DeployedToTest"] },
  { key: "qa", label: "QA / UAT", statuses: ["QaInProgress", "QaPassed", "QaFailed", "UatInProgress", "UatRejected", "UatAccepted"] },
  { key: "release", label: "Release", statuses: ["ScheduledForProduction", "DeployedToProduction"] },
  { key: "closed", label: "Closed", statuses: ["Closed"] },
];

export default function TicketsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) loadTickets();
    else setLoading(false);
  }, [token, workspaceId]);

  const loadTickets = async () => {
    try {
      const data = await api<{ items: Ticket[] }>(
        `/workspaces/${workspaceId}/tickets?page=1&pageSize=100`, {}, token, orgId, workspaceId
      );
      setTickets(data.items || []);
    } catch (err) {
      console.error(err);
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
            <h1 className="text-2xl font-bold text-zinc-100">Ticket Board</h1>
            <p className="text-zinc-400 mt-1">Track changes from request to production</p>
          </div>
          <Link href="/tickets/new"><Button><Plus className="h-4 w-4" /> New Ticket</Button></Link>
        </div>

        {!workspaceId ? (
          <EmptyState title="No workspace" description="Create a workspace to manage tickets." actionLabel="Go to Dashboard" actionHref="/" />
        ) : loading ? (
          <PageSkeleton rows={6} />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 overflow-x-auto">
            {columns.map((col) => {
              const colTickets = tickets.filter((t) => col.statuses.includes(t.status));
              return (
                <div key={col.key} className="space-y-3 min-w-[200px]">
                  <div className="flex items-center justify-between px-2">
                    <h3 className="text-sm font-medium text-zinc-400">{col.label}</h3>
                    <span className="text-xs text-zinc-500">{colTickets.length}</span>
                  </div>
                  <div className="space-y-2 min-h-[200px]">
                    {colTickets.map((ticket) => (
                      <Link key={ticket.id} href={`/tickets/${ticket.id}`}>
                        <Card className="p-4 hover:border-indigo-500/30 transition-colors cursor-pointer">
                          <div className="flex items-center gap-2 mb-2">
                            <span className="text-xs text-zinc-500">#{ticket.ticketNumber}</span>
                            <Badge variant={statusColors[ticket.status] || "neutral"}>{ticket.priority}</Badge>
                          </div>
                          <p className="text-sm font-medium text-zinc-200 line-clamp-2">{ticket.title}</p>
                          <p className="text-xs text-zinc-500 mt-2">{ticket.status}</p>
                        </Card>
                      </Link>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
