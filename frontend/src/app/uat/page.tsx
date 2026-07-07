"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { usePendingUat } from "@/lib/api-hooks";
import { CheckCircle, XCircle } from "lucide-react";

interface Ticket {
  id: string;
  ticketNumber: number;
  title: string;
  status: string;
  priority: string;
}

export default function UatPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const { data: tickets = [], isLoading, refetch } = usePendingUat() as { data: Ticket[]; isLoading: boolean; refetch: () => void };

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  const submitUat = async (ticketId: string, decision: string) => {
    try {
      await api(`/tickets/${ticketId}/uat`, {
        method: "POST",
        body: JSON.stringify({ decision, comments: "" }),
      }, token, orgId, workspaceId);
      showToast(decision === "Approved" ? "UAT accepted" : "UAT rejected", decision === "Approved" ? "success" : "error");
      refetch();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "UAT submission failed", "error");
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">UAT Queue</h1>
          <p className="text-zinc-400 mt-1">User acceptance testing approvals</p>
        </div>

        {isLoading ? <PageSkeleton rows={3} /> : tickets.length === 0 ? (
          <EmptyState
            title="No tickets awaiting UAT"
            description="Tickets that passed QA will appear here for business sign-off."
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
                </div>
                <div className="flex gap-2">
                  <Button size="sm" variant="secondary" onClick={() => submitUat(t.id, "Approved")}>
                    <CheckCircle className="h-4 w-4" /> Accept
                  </Button>
                  <Button size="sm" variant="destructive" onClick={() => submitUat(t.id, "Rejected")}>
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
