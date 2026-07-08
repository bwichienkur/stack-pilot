"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useDeployments } from "@/lib/api-hooks";

export default function DeploymentsPage() {
  const { token } = useAuth();
  const router = useRouter();
  const { data: runs = [], isLoading, error } = useDeployments();

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Deployments</h1>
          <p className="text-zinc-400 mt-1">CI/CD build runs from GitHub Actions, Jenkins, and Azure Pipelines</p>
        </div>

        {error && <p className="text-sm text-red-400">{error instanceof Error ? error.message : "Failed to load"}</p>}

        {isLoading ? <PageSkeleton rows={4} /> : runs.length === 0 ? (
          <EmptyState
            title="No build runs yet"
            description="Sync a CI/CD connector (GitHub Actions, Jenkins, or Azure Pipelines) or configure GitHub webhooks"
          />
        ) : (
          <div className="space-y-3">
            {runs.map((run) => (
              <Card key={run.id} className="p-4 flex items-center justify-between gap-4">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <Badge variant={run.status === "Completed" ? "success" : run.status === "Failed" ? "danger" : "warning"}>
                      {run.status}
                    </Badge>
                    {run.conclusion && <span className="text-xs text-zinc-500">{run.conclusion}</span>}
                  </div>
                  <p className="text-sm text-zinc-300">
                    {run.startedAt ? new Date(run.startedAt).toLocaleString() : "Pending"}
                  </p>
                </div>
                {run.logsUrl && (
                  <a href={run.logsUrl} target="_blank" rel="noopener noreferrer" className="text-sm text-indigo-400 hover:text-indigo-300">
                    View logs
                  </a>
                )}
              </Card>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
