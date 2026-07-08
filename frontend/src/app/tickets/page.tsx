"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { useTickets } from "@/lib/api-hooks";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
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
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const { data, isLoading } = useTickets();
  const tickets = (data?.items ?? []) as Ticket[];
  const [draggingId, setDraggingId] = useState<string | null>(null);
  const [dropColumn, setDropColumn] = useState<string | null>(null);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  const moveTicket = async (ticket: Ticket, targetStatuses: string[]) => {
    if (!token || !orgId || !workspaceId) return;
    if (targetStatuses.includes(ticket.status)) return;

    for (const status of targetStatuses) {
      try {
        await api(`/tickets/${ticket.id}`, {
          method: "PATCH",
          body: JSON.stringify({ status }),
        }, token, orgId, workspaceId);
        await queryClient.invalidateQueries({ queryKey: ["tickets", workspaceId] });
        showToast(`Moved to ${status}`, "success");
        return;
      } catch {
        // try next status in column
      }
    }
    showToast("Invalid status transition for this ticket", "error");
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Ticket Board</h1>
            <p className="text-zinc-400 mt-1">Drag tickets between columns to update workflow status</p>
          </div>
          <Link href="/tickets/new"><Button><Plus className="h-4 w-4" /> New Ticket</Button></Link>
        </div>

        {!workspaceId ? (
          <EmptyState title="No workspace" description="Create a workspace to manage tickets." actionLabel="Go to Dashboard" actionHref="/" />
        ) : isLoading ? (
          <PageSkeleton rows={6} />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 overflow-x-auto">
            {columns.map((col) => {
              const colTickets = tickets.filter((t) => col.statuses.includes(t.status));
              return (
                <div
                  key={col.key}
                  className={`space-y-3 min-w-[200px] rounded-lg p-1 transition-colors ${dropColumn === col.key ? "bg-indigo-500/10 ring-1 ring-indigo-500/40" : ""}`}
                  onDragOver={(e) => { e.preventDefault(); setDropColumn(col.key); }}
                  onDragLeave={() => setDropColumn((c) => (c === col.key ? null : c))}
                  onDrop={(e) => {
                    e.preventDefault();
                    setDropColumn(null);
                    const ticketId = e.dataTransfer.getData("text/ticket-id");
                    const ticket = tickets.find((t) => t.id === ticketId);
                    if (ticket) void moveTicket(ticket, col.statuses);
                    setDraggingId(null);
                  }}
                >
                  <div className="flex items-center justify-between px-2">
                    <h3 className="text-sm font-medium text-zinc-400">{col.label}</h3>
                    <span className="text-xs text-zinc-500">{colTickets.length}</span>
                  </div>
                  <div className="space-y-2 min-h-[200px]">
                    {colTickets.map((ticket) => (
                      <div
                        key={ticket.id}
                        draggable
                        onDragStart={(e) => {
                          e.dataTransfer.setData("text/ticket-id", ticket.id);
                          setDraggingId(ticket.id);
                        }}
                        onDragEnd={() => setDraggingId(null)}
                        className={draggingId === ticket.id ? "opacity-50" : ""}
                      >
                        <Link href={`/tickets/${ticket.id}`}>
                          <Card className="p-4 hover:border-indigo-500/30 transition-colors cursor-grab active:cursor-grabbing">
                            <div className="flex items-center gap-2 mb-2">
                              <span className="text-xs text-zinc-500">#{ticket.ticketNumber}</span>
                              <Badge variant={statusColors[ticket.status] || "neutral"}>{ticket.priority}</Badge>
                            </div>
                            <p className="text-sm font-medium text-zinc-200 line-clamp-2">{ticket.title}</p>
                            <p className="text-xs text-zinc-500 mt-2">{ticket.status}</p>
                          </Card>
                        </Link>
                      </div>
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
