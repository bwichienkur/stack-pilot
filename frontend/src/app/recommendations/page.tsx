"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { useRecommendations } from "@/lib/api-hooks";
import { Sparkles } from "lucide-react";

export default function RecommendationsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const { data, isLoading, error, refetch } = useRecommendations();
  const [generating, setGenerating] = useState(false);

  const items = data?.items ?? [];

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  const generate = async () => {
    if (!workspaceId) return;
    setGenerating(true);
    try {
      const created = await api<unknown[]>(
        `/workspaces/${workspaceId}/recommendations/generate`, { method: "POST" }, token, orgId, workspaceId
      );
      showToast(`Generated ${created.length} recommendation(s)`, "success");
      refetch();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Generation failed", "error");
    } finally {
      setGenerating(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">AI Recommendations</h1>
            <p className="text-zinc-400 mt-1">Continuous improvement suggestions from your knowledge graph</p>
          </div>
          <Button onClick={generate} disabled={generating || !workspaceId}>
            <Sparkles className="h-4 w-4" /> {generating ? "Analyzing..." : "Generate"}
          </Button>
        </div>

        {error && <p className="text-sm text-red-400">{error instanceof Error ? error.message : "Failed to load"}</p>}

        {isLoading ? <PageSkeleton rows={4} /> : items.length === 0 ? (
          <EmptyState
            title="No recommendations yet"
            description="Scan repositories to build risk scores, then generate AI recommendations."
            actionLabel="Go to Connectors"
            actionHref="/connectors"
          />
        ) : (
          <div className="space-y-3">
            {items.map((r) => (
              <Card key={r.id} className="p-4">
                <div className="flex items-center gap-2 mb-2">
                  <Badge variant="neutral">{r.type}</Badge>
                  <Badge variant={r.riskLevel === "Critical" || r.riskLevel === "High" ? "danger" : "warning"}>{r.riskLevel}</Badge>
                  <Badge>{r.status}</Badge>
                </div>
                <p className="text-zinc-100">{r.summary}</p>
                {r.confidenceScore !== undefined && (
                  <p className="text-xs text-zinc-500 mt-2">Confidence: {(r.confidenceScore * 100).toFixed(0)}%</p>
                )}
              </Card>
            ))}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
