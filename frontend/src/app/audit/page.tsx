"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useAuditLogs } from "@/lib/api-hooks";

export default function AuditPage() {
  const { token } = useAuth();
  const router = useRouter();
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const { data, isLoading, error } = useAuditLogs(page, pageSize);

  const logs = data?.items ?? [];
  const total = data?.totalCount ?? 0;

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  if (!token) return null;

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Audit Logs</h1>
          <p className="text-zinc-400 mt-1">Immutable trail of organization activity</p>
        </div>

        {error && <p className="text-sm text-red-400">{error instanceof Error ? error.message : "Failed to load"}</p>}

        {isLoading ? <PageSkeleton rows={5} /> : logs.length === 0 ? (
          <EmptyState title="No audit entries" description="Actions performed in this organization will appear here." />
        ) : (
          <>
            <Card className="overflow-hidden overflow-x-auto">
              <table className="w-full text-sm min-w-[640px]">
                <thead className="bg-zinc-900/80 text-zinc-400 text-left">
                  <tr>
                    <th className="px-4 py-3 font-medium">Time</th>
                    <th className="px-4 py-3 font-medium">Action</th>
                    <th className="px-4 py-3 font-medium">Entity</th>
                    <th className="px-4 py-3 font-medium">User</th>
                  </tr>
                </thead>
                <tbody>
                  {logs.map((log) => (
                    <tr key={log.id} className="border-t border-zinc-800 hover:bg-zinc-900/50">
                      <td className="px-4 py-3 text-zinc-400 whitespace-nowrap">
                        {new Date(log.createdAt).toLocaleString()}
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant="neutral">{log.action}</Badge>
                      </td>
                      <td className="px-4 py-3 text-zinc-300">
                        {log.entityType || "—"}
                        {log.entityId && <span className="text-zinc-500 text-xs block">{log.entityId.slice(0, 8)}…</span>}
                      </td>
                      <td className="px-4 py-3 text-zinc-400 text-xs font-mono">
                        {log.userId ? `${log.userId.slice(0, 8)}…` : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Card>

            <div className="flex items-center justify-between flex-wrap gap-2">
              <p className="text-sm text-zinc-500">{total} total entries</p>
              <div className="flex gap-2">
                <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
                <span className="text-sm text-zinc-400 self-center">Page {page} of {totalPages}</span>
                <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
              </div>
            </div>
          </>
        )}
      </div>
    </AppLayout>
  );
}
