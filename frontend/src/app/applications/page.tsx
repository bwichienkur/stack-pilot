"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useApplications } from "@/lib/api-hooks";

export default function ApplicationsPage() {
  const { token } = useAuth();
  const router = useRouter();
  const { data: apps = [], isLoading, error } = useApplications();

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Applications</h1>
          <p className="text-zinc-400 mt-1">Application inventory from repository scans</p>
        </div>

        {error && <p className="text-sm text-red-400">{error instanceof Error ? error.message : "Failed to load"}</p>}

        {isLoading ? <PageSkeleton rows={4} /> : apps.length === 0 ? (
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
