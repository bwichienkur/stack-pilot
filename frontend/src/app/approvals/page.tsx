"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { CheckCircle, XCircle } from "lucide-react";

interface Ticket {
  id: string;
  ticketNumber: number;
  title: string;
  status: string;
  priority: string;
  riskScore?: number;
}

export default function ApprovalsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) loadPending();
  }, [token, workspaceId]);

  const loadPending = async () => {
    try {
      const data = await api<Ticket[]>(`/workspaces/${workspaceId}/approvals/pending`, {}, token, orgId, workspaceId);
      setTickets(data);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load approvals", "error");
    } finally {
      setLoading(false);
    }
  };

  const decide = async (ticketId: string, decision: string) => {
    try {
      await api(`/tickets/${ticketId}/approvals`, {
        method: "POST",
        body: JSON.stringify({ approvalType: "TechnicalReviewer", decision, comments: "" }),
      }, token, orgId, workspaceId);
      showToast(decision === "Approved" ? "Ticket approved" : "Ticket rejected", "success");
      loadPending();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Approval failed", "error");
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Approval Queue</h1>
          <p className="text-zinc-400 mt-1">Review AI-generated requirements before implementation</p>
        </div>

        {loading ? <PageSkeleton rows={3} /> : tickets.length === 0 ? (
          <EmptyState
            title="No pending approvals"
            description="Tickets awaiting approval will appear here after AI requirements are generated."
            actionLabel="View Tickets"
            actionHref="/tickets"
          />
        ) : (
          <div className="space-y-3">
            {tickets.map((t) => (
              <Card key={t.id} className="p-4 flex items-center justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-xs text-zinc-500">#{t.ticketNumber}</span>
                    <Badge variant="warning">{t.status}</Badge>
                    <Badge>{t.priority}</Badge>
                  </div>
                  <Link href={`/tickets/${t.id}`} className="text-zinc-100 font-medium hover:text-indigo-400">{t.title}</Link>
                  {t.riskScore !== undefined && <p className="text-xs text-zinc-500 mt-1">Risk: {t.riskScore}</p>}
                </div>
                <div className="flex gap-2">
                  <Button size="sm" variant="secondary" onClick={() => decide(t.id, "Approved")}>
                    <CheckCircle className="h-4 w-4" /> Approve
                  </Button>
                  <Button size="sm" variant="destructive" onClick={() => decide(t.id, "Rejected")}>
                    <XCircle className="h-4 w-4" /> Reject
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
