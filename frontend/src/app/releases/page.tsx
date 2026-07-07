"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useToast } from "@/components/ui/toast";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/utils";
import { Calendar } from "lucide-react";

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
                    <Card key={r.id} className="p-4 flex items-start justify-between gap-4">
                      <div>
                        <div className="flex items-center gap-2 mb-1">
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
