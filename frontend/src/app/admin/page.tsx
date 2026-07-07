"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { EmptyState } from "@/components/empty-state";
import { useAuth } from "@/lib/auth-context";
import { useAdminOrganizations, useIsSuperAdmin } from "@/lib/api-hooks";
import { Shield } from "lucide-react";

export default function AdminPage() {
  const { token } = useAuth();
  const router = useRouter();
  const { data: isSuperAdmin, isLoading: checkingAdmin } = useIsSuperAdmin();
  const { data: orgs = [], isLoading, error } = useAdminOrganizations(isSuperAdmin === true);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  useEffect(() => {
    if (!checkingAdmin && isSuperAdmin === false) {
      router.replace("/");
    }
  }, [checkingAdmin, isSuperAdmin, router]);

  if (!token) return null;

  if (checkingAdmin || isSuperAdmin === undefined) {
    return <AppLayout><PageSkeleton rows={4} /></AppLayout>;
  }

  if (!isSuperAdmin) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100 flex items-center gap-2">
            <Shield className="h-6 w-6 text-indigo-400" />
            Platform Admin
          </h1>
          <p className="text-zinc-400 mt-1">Cross-tenant organization overview</p>
        </div>

        {error && <p className="text-sm text-red-400">{error instanceof Error ? error.message : "Failed to load organizations"}</p>}

        {isLoading ? (
          <PageSkeleton rows={5} />
        ) : orgs.length === 0 ? (
          <EmptyState title="No organizations" description="Organizations will appear here for platform super admins." />
        ) : (
          <Card className="overflow-hidden overflow-x-auto">
            <table className="w-full text-sm min-w-[720px]">
              <thead className="bg-zinc-900/80 text-zinc-400 text-left">
                <tr>
                  <th className="px-4 py-3 font-medium">Organization</th>
                  <th className="px-4 py-3 font-medium">Plan</th>
                  <th className="px-4 py-3 font-medium">Members</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Created</th>
                </tr>
              </thead>
              <tbody>
                {orgs.map((org) => (
                  <tr key={org.id} className="border-t border-zinc-800 hover:bg-zinc-900/50">
                    <td className="px-4 py-3">
                      <p className="text-zinc-200 font-medium">{org.name}</p>
                      <p className="text-xs text-zinc-500">{org.slug}</p>
                    </td>
                    <td className="px-4 py-3"><Badge variant="neutral">{org.plan}</Badge></td>
                    <td className="px-4 py-3 text-zinc-300">{org.memberCount}</td>
                    <td className="px-4 py-3">
                      <Badge variant={org.isActive ? "success" : "danger"}>{org.isActive ? "Active" : "Inactive"}</Badge>
                    </td>
                    <td className="px-4 py-3 text-zinc-400 whitespace-nowrap">
                      {new Date(org.createdAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
      </div>
    </AppLayout>
  );
}
