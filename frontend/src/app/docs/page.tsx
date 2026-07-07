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

interface DocPage {
  id: string;
  title: string;
  docType: string;
  latestVersion: number;
  status: string;
}

export default function DocsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [pages, setPages] = useState<DocPage[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) load();
  }, [token, workspaceId]);

  const load = async () => {
    try {
      const data = await api<{ items: DocPage[] }>(
        `/workspaces/${workspaceId}/docs?page=1&pageSize=50`, {}, token, orgId, workspaceId
      );
      setPages(data.items);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load documentation", "error");
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Documentation Hub</h1>
          <p className="text-zinc-400 mt-1">Versioned documentation linked to your knowledge graph</p>
        </div>

        {loading ? <PageSkeleton rows={4} /> : pages.length === 0 ? (
          <EmptyState
            title="No documentation pages"
            description="Documentation is generated from graph nodes and tickets. Create tickets and approve requirements to start."
            actionLabel="View Tickets"
            actionHref="/tickets"
          />
        ) : (
          <div className="space-y-3">
            {pages.map((p) => (
              <Card key={p.id} className="p-4 flex items-center justify-between">
                <div>
                  <p className="font-medium text-zinc-100">{p.title}</p>
                  <p className="text-xs text-zinc-500 mt-1">{p.docType} · v{p.latestVersion}</p>
                </div>
                <Badge variant="neutral">{p.status}</Badge>
              </Card>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
