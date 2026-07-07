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
import { Sparkles } from "lucide-react";

interface Recommendation {
  id: string;
  type: string;
  summary: string;
  riskLevel: string;
  confidenceScore?: number;
  status: string;
  createdAt: string;
}

export default function RecommendationsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [items, setItems] = useState<Recommendation[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) load();
  }, [token, workspaceId]);

  const load = async () => {
    try {
      const data = await api<{ items: Recommendation[] }>(
        `/workspaces/${workspaceId}/recommendations?page=1&pageSize=50`, {}, token, orgId, workspaceId
      );
      setItems(data.items);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load recommendations", "error");
    } finally {
      setLoading(false);
    }
  };

  const generate = async () => {
    setGenerating(true);
    try {
      const created = await api<Recommendation[]>(
        `/workspaces/${workspaceId}/recommendations/generate`, { method: "POST" }, token, orgId, workspaceId
      );
      showToast(`Generated ${created.length} recommendation(s)`, "success");
      load();
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

        {loading ? <PageSkeleton rows={4} /> : items.length === 0 ? (
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
