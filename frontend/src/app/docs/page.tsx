"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useToast } from "@/components/ui/toast";
import { useDocs, useGenerateDoc } from "@/lib/api-hooks";
import { Bot } from "lucide-react";

export default function DocsPage() {
  const { token } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const { data, isLoading } = useDocs();
  const generateDoc = useGenerateDoc();
  const [generatingId, setGeneratingId] = useState<string | null>(null);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  const handleGenerate = async (pageId: string) => {
    setGeneratingId(pageId);
    try {
      await generateDoc.mutateAsync(pageId);
      showToast("Documentation generated", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Generation failed", "error");
    } finally {
      setGeneratingId(null);
    }
  };

  const pages = data?.items ?? [];

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Documentation Hub</h1>
          <p className="text-zinc-400 mt-1">Versioned documentation linked to your knowledge graph</p>
        </div>

        {isLoading ? <PageSkeleton rows={4} /> : pages.length === 0 ? (
          <EmptyState
            title="No documentation pages"
            description="Documentation is generated from graph nodes and tickets. Create tickets and approve requirements to start."
            actionLabel="View Tickets"
            actionHref="/tickets"
          />
        ) : (
          <div className="space-y-3">
            {pages.map((p) => (
              <Card key={p.id} className="p-4 flex items-center justify-between gap-4">
                <div>
                  <p className="font-medium text-zinc-100">{p.title}</p>
                  <p className="text-xs text-zinc-500 mt-1">{p.docType} · v{p.latestVersion}</p>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="neutral">{p.status}</Badge>
                  <Button
                    size="sm"
                    variant="secondary"
                    disabled={generatingId === p.id}
                    onClick={() => handleGenerate(p.id)}
                  >
                    <Bot className="h-4 w-4" />
                    {generatingId === p.id ? "Generating…" : "Generate"}
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
