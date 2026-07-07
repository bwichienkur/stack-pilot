"use client";

import { useEffect, useState } from "react";
import { useRouter, useParams } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, CardHeader, CardTitle, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { MarkdownViewer } from "@/components/markdown-viewer";
import { useAuth } from "@/lib/auth-context";
import { useDocPage } from "@/lib/api-hooks";
import { ArrowLeft, History } from "lucide-react";

export default function DocViewerPage() {
  const { pageId } = useParams<{ pageId: string }>();
  const { token } = useAuth();
  const router = useRouter();
  const { data, isLoading, error } = useDocPage(pageId);
  const [selectedVersion, setSelectedVersion] = useState<number | null>(null);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  useEffect(() => {
    if (data?.version) setSelectedVersion(data.version);
  }, [data?.version]);

  if (!token) return null;

  const versions = data?.versions?.length
    ? data.versions
    : data
      ? [{ version: data.version, generatedBy: data.generatedBy, status: data.status, createdAt: data.createdAt }]
      : [];

  const activeVersion = versions.find((v) => v.version === selectedVersion) ?? versions[0];
  const showContent = selectedVersion === data?.version ? data.contentMd : data?.contentMd;

  return (
    <AppLayout>
      <div className="space-y-6 max-w-4xl">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <Link href="/docs" className="inline-flex items-center gap-1 text-sm text-zinc-400 hover:text-indigo-400 mb-2">
              <ArrowLeft className="h-4 w-4" /> Back to docs
            </Link>
            <h1 className="text-2xl font-bold text-zinc-100">{data?.title || "Documentation"}</h1>
            {activeVersion && (
              <p className="text-sm text-zinc-400 mt-1">
                Version {activeVersion.version} · {activeVersion.generatedBy} · {new Date(activeVersion.createdAt).toLocaleString()}
              </p>
            )}
          </div>
          {activeVersion && <Badge variant="neutral">{activeVersion.status}</Badge>}
        </div>

        {isLoading ? (
          <PageSkeleton rows={6} />
        ) : error ? (
          <Card className="p-6">
            <p className="text-red-400">{error instanceof Error ? error.message : "Failed to load documentation"}</p>
          </Card>
        ) : !data ? (
          <Card className="p-6">
            <p className="text-zinc-400">Documentation not found.</p>
          </Card>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
            <Card className="lg:col-span-1 p-4 h-fit">
              <CardHeader className="p-0 pb-3">
                <CardTitle className="text-sm flex items-center gap-2">
                  <History className="h-4 w-4 text-indigo-400" />
                  Version History
                </CardTitle>
              </CardHeader>
              <CardContent className="p-0 space-y-1">
                {versions.map((v) => (
                  <button
                    key={v.version}
                    type="button"
                    onClick={() => setSelectedVersion(v.version)}
                    className={`w-full text-left px-3 py-2 rounded-lg text-sm transition-colors ${
                      selectedVersion === v.version
                        ? "bg-indigo-500/10 text-indigo-400 border border-indigo-500/20"
                        : "text-zinc-400 hover:bg-zinc-800/50 hover:text-zinc-200"
                    }`}
                  >
                    <span className="font-medium">v{v.version}</span>
                    <span className="block text-xs text-zinc-500 mt-0.5">{new Date(v.createdAt).toLocaleDateString()}</span>
                  </button>
                ))}
              </CardContent>
            </Card>

            <Card className="lg:col-span-3 p-6">
              <MarkdownViewer content={showContent || ""} />
            </Card>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
