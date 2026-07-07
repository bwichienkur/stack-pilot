"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";

interface BuildRun {
  id: string;
  status: string;
  conclusion?: string;
  logsUrl?: string;
  pullRequestUrl?: string;
  startedAt?: string;
  completedAt?: string;
}

export default function DeploymentsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [runs, setRuns] = useState<BuildRun[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) load();
  }, [token, workspaceId]);

  const load = async () => {
    try {
      const data = await api<BuildRun[]>(`/workspaces/${workspaceId}/build-runs`, {}, token, orgId, workspaceId);
      setRuns(data);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load build runs", "error");
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Deployments</h1>
          <p className="text-zinc-400 mt-1">CI/CD build runs from GitHub Actions webhooks</p>
        </div>

        {loading ? <PageSkeleton rows={4} /> : runs.length === 0 ? (
          <EmptyState
            title="No build runs yet"
            description="Configure a GitHub Actions connector and point webhooks to POST /api/v1/webhooks/github"
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
