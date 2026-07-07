"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { Plus } from "lucide-react";

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
  Submitted: "neutral",
  AiAnalysisPending: "default",
  RequirementsDrafted: "default",
  AwaitingApproval: "warning",
  Approved: "success",
  QaInProgress: "default",
  QaPassed: "success",
  QaFailed: "danger",
  UatAccepted: "success",
  Closed: "neutral",
};

const columns = [
  { key: "Submitted", label: "Submitted" },
  { key: "AwaitingApproval", label: "Awaiting Approval" },
  { key: "Approved", label: "Approved" },
  { key: "QaInProgress", label: "QA" },
  { key: "UatAccepted", label: "UAT Accepted" },
  { key: "Closed", label: "Closed" },
];

export default function TicketsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const [tickets, setTickets] = useState<Ticket[]>([]);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) loadTickets();
  }, [token, workspaceId]);

  const loadTickets = async () => {
    try {
      const data = await api<{ items: Ticket[] }>(
        `/workspaces/${workspaceId}/tickets?page=1&pageSize=50`, {}, token, orgId, workspaceId
      );
      setTickets(data.items || []);
    } catch (err) {
      console.error(err);
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

        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6 gap-4">
          {columns.map((col) => (
            <div key={col.key} className="space-y-3">
              <div className="flex items-center justify-between px-2">
                <h3 className="text-sm font-medium text-zinc-400">{col.label}</h3>
                <span className="text-xs text-zinc-500">{tickets.filter((t) => t.status === col.key).length}</span>
              </div>
              <div className="space-y-2 min-h-[200px]">
                {tickets.filter((t) => t.status === col.key).map((ticket) => (
                  <Link key={ticket.id} href={`/tickets/${ticket.id}`}>
                    <Card className="p-4 hover:border-indigo-500/30 transition-colors cursor-pointer">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="text-xs text-zinc-500">#{ticket.ticketNumber}</span>
                        <Badge variant={statusColors[ticket.status] || "neutral"}>{ticket.priority}</Badge>
                      </div>
                      <p className="text-sm font-medium text-zinc-200 line-clamp-2">{ticket.title}</p>
                      <p className="text-xs text-zinc-500 mt-2">{ticket.ticketType}</p>
                    </Card>
                  </Link>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </AppLayout>
  );
}
