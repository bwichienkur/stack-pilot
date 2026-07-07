"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button, Input } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { usePendingQa } from "@/lib/api-hooks";
import { CheckCircle, XCircle } from "lucide-react";

interface Ticket {
  id: string;
  ticketNumber: number;
  title: string;
  status: string;
  priority: string;
}

export default function QaPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const { data: tickets = [], isLoading, refetch } = usePendingQa() as { data: Ticket[]; isLoading: boolean; refetch: () => void };
  const [evidenceByTicket, setEvidenceByTicket] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  const submitQa = async (ticketId: string, result: string) => {
    try {
      const evidenceRaw = evidenceByTicket[ticketId]?.trim();
      const evidenceUrls = evidenceRaw
        ? evidenceRaw.split(",").map((u) => u.trim()).filter(Boolean)
        : undefined;
      await api(`/tickets/${ticketId}/qa`, {
        method: "POST",
        body: JSON.stringify({
          result,
          notes: result === "pass" ? "QA passed" : "QA failed — needs fixes",
          evidenceUrls,
        }),
      }, token, orgId, workspaceId);
      showToast(result === "pass" ? "QA passed" : "QA failed", result === "pass" ? "success" : "error");
      refetch();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "QA submission failed", "error");
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">QA Queue</h1>
          <p className="text-zinc-400 mt-1">Tickets awaiting quality assurance testing</p>
        </div>

        {isLoading ? <PageSkeleton rows={3} /> : tickets.length === 0 ? (
          <EmptyState
            title="No tickets in QA"
            description="Tickets deployed to test will appear here for QA review."
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
                  <Input
                    className="mt-2"
                    placeholder="Evidence URLs (comma-separated)"
                    value={evidenceByTicket[t.id] ?? ""}
                    onChange={(e) => setEvidenceByTicket((prev) => ({ ...prev, [t.id]: e.target.value }))}
                  />
                </div>
                <div className="flex gap-2">
                  <Button size="sm" variant="secondary" onClick={() => submitQa(t.id, "pass")}>
                    <CheckCircle className="h-4 w-4" /> Pass
                  </Button>
                  <Button size="sm" variant="destructive" onClick={() => submitQa(t.id, "fail")}>
                    <XCircle className="h-4 w-4" /> Fail
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
