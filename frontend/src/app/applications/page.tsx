"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";

interface Application {
  id: string;
  nodeType: string;
  name: string;
  riskScore?: number;
  metadataJson?: string;
}

export default function ApplicationsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [apps, setApps] = useState<Application[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) load();
  }, [token, workspaceId]);

  const load = async () => {
    try {
      const data = await api<Application[]>(`/workspaces/${workspaceId}/applications`, {}, token, orgId, workspaceId);
      setApps(data);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load applications", "error");
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Applications</h1>
          <p className="text-zinc-400 mt-1">Application inventory from repository scans</p>
        </div>

        {loading ? <PageSkeleton rows={4} /> : apps.length === 0 ? (
          <EmptyState
            title="No applications discovered"
            description="Connect and sync a repository to populate your application inventory."
            actionLabel="Connectors"
            actionHref="/connectors"
          />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {apps.map((app) => (
              <Card key={app.id} className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <Link href="/architecture" className="font-medium text-zinc-100 hover:text-indigo-400">{app.name}</Link>
                    <p className="text-xs text-zinc-500 mt-1">{app.nodeType}</p>
                  </div>
                  {app.riskScore !== undefined && app.riskScore > 0 && (
                    <Badge variant={app.riskScore > 5 ? "danger" : "warning"}>Risk {app.riskScore}</Badge>
                  )}
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
