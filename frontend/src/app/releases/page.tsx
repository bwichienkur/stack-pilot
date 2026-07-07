"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useToast } from "@/components/ui/toast";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/utils";
import { Calendar, Rocket, RotateCcw, CheckCircle2 } from "lucide-react";

interface Release {
  id: string;
  ticketId: string;
  ticketNumber: number;
  ticketTitle: string;
  ticketStatus: string;
  scheduledAt: string;
  releaseWindow?: string;
  status: string;
}

function groupByDate(releases: Release[]): Record<string, Release[]> {
  return releases.reduce<Record<string, Release[]>>((acc, r) => {
    const key = new Date(r.scheduledAt).toLocaleDateString(undefined, {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });
    (acc[key] ??= []).push(r);
    return acc;
  }, {});
}

export default function ReleasesPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const queryClient = useQueryClient();
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const { data: releases = [], isLoading, error } = useQuery({
    queryKey: ["releases", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<Release[]>(`/workspaces/${workspaceId}/releases`, {}, token, orgId, workspaceId),
  });

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  useEffect(() => {
    if (error) showToast(error instanceof Error ? error.message : "Failed to load releases", "error");
  }, [error, showToast]);

  const patchRelease = async (releaseId: string, action: "deploy" | "verify" | "rollback") => {
    setActionLoading(`${releaseId}-${action}`);
    try {
      await api(`/releases/${releaseId}`, {
        method: "PATCH",
        body: JSON.stringify({ action }),
      }, token, orgId, workspaceId);
      showToast(`Release ${action} initiated`, "success");
      queryClient.invalidateQueries({ queryKey: ["releases", workspaceId] });
    } catch (err) {
      showToast(err instanceof Error ? err.message : `${action} failed`, "error");
    } finally {
      setActionLoading(null);
    }
  };

  if (!token) return null;

  const grouped = groupByDate(releases);
  const dates = Object.keys(grouped);

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100 flex items-center gap-2">
            <Calendar className="h-6 w-6 text-indigo-400" />
            Release Calendar
          </h1>
          <p className="text-zinc-400 mt-1">Scheduled production releases linked to approved tickets</p>
        </div>

        {isLoading ? (
          <PageSkeleton rows={4} />
        ) : releases.length === 0 ? (
          <EmptyState
            title="No scheduled releases"
            description="Schedule a release from a ticket after UAT acceptance using the schedule-release action."
            actionLabel="View Tickets"
            actionHref="/tickets"
          />
        ) : (
          <div className="space-y-8">
            {dates.map((date) => (
              <div key={date}>
                <h2 className="text-sm font-medium text-zinc-400 mb-3">{date}</h2>
                <div className="space-y-3">
                  {grouped[date].map((r) => (
                    <Card key={r.id} className="p-4 flex flex-col sm:flex-row items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2 mb-1 flex-wrap">
                          <span className="text-xs text-zinc-500">#{r.ticketNumber}</span>
                          <Badge variant="warning">{r.status}</Badge>
                          <Badge variant="neutral">{r.ticketStatus}</Badge>
                        </div>
                        <Link href={`/tickets/${r.ticketId}`} className="font-medium text-zinc-100 hover:text-indigo-400">
                          {r.ticketTitle}
                        </Link>
                        <p className="text-sm text-zinc-400 mt-2">
                          {new Date(r.scheduledAt).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" })}
                          {r.releaseWindow && ` · ${r.releaseWindow}`}
                        </p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          size="sm"
                          disabled={!!actionLoading}
                          onClick={() => patchRelease(r.id, "deploy")}
                        >
                          <Rocket className="h-3 w-3" />
                          {actionLoading === `${r.id}-deploy` ? "..." : "Deploy"}
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          disabled={!!actionLoading}
                          onClick={() => patchRelease(r.id, "verify")}
                        >
                          <CheckCircle2 className="h-3 w-3" />
                          {actionLoading === `${r.id}-verify` ? "..." : "Verify"}
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          disabled={!!actionLoading}
                          onClick={() => patchRelease(r.id, "rollback")}
                        >
                          <RotateCcw className="h-3 w-3" />
                          {actionLoading === `${r.id}-rollback` ? "..." : "Rollback"}
                        </Button>
                      </div>
                    </Card>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
