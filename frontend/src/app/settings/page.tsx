"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Button, Input } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { STUB_NAV_FLAGS } from "@/lib/feature-flags";

interface Settings {
  id: string;
  name: string;
  slug: string;
  plan: string;
  featureFlags: Record<string, boolean>;
}

interface Member {
  userId: string;
  email: string;
  firstName?: string;
  lastName?: string;
  roleName: string;
  joinedAt: string;
}

const FLAG_LABELS: Record<string, string> = {
  applications: "Applications module",
  docs: "Documentation hub",
  recommendations: "Recommendations",
  qa: "QA queue",
  uat: "UAT queue",
  deployments: "Deployments",
};

export default function SettingsPage() {
  const { token, orgId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [settings, setSettings] = useState<Settings | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [name, setName] = useState("");
  const [flags, setFlags] = useState<Record<string, boolean>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (orgId) load();
  }, [token, orgId]);

  const load = async () => {
    try {
      const [s, m] = await Promise.all([
        api<Settings>(`/organizations/${orgId}/settings`, {}, token, orgId),
        api<Member[]>(`/organizations/${orgId}/members`, {}, token, orgId).catch(() => []),
      ]);
      setSettings(s);
      setName(s.name);
      setFlags(s.featureFlags || {});
      setMembers(m);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load settings", "error");
    } finally {
      setLoading(false);
    }
  };

  const save = async () => {
    setSaving(true);
    try {
      const updated = await api<Settings>(`/organizations/${orgId}/settings`, {
        method: "PUT",
        body: JSON.stringify({ name, featureFlags: flags }),
      }, token, orgId);
      setSettings(updated);
      showToast("Settings saved", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Save failed", "error");
    } finally {
      setSaving(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6 max-w-3xl">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Settings</h1>
          <p className="text-zinc-400 mt-1">Organization configuration and feature modules</p>
        </div>

        {loading ? <PageSkeleton rows={4} /> : (
          <>
            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Organization</h2>
              <div>
                <label className="text-sm text-zinc-400 mb-1 block">Name</label>
                <Input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <p className="text-xs text-zinc-500">Slug: {settings?.slug} · Plan: {settings?.plan}</p>
            </Card>

            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Feature modules</h2>
              <p className="text-sm text-zinc-400">Enable preview modules in the sidebar. Core workflows (connectors, architecture, tickets, approvals) are always available.</p>
              <div className="space-y-3">
                {STUB_NAV_FLAGS.map((key) => (
                  <label key={key} className="flex items-center justify-between gap-4 py-2 border-b border-zinc-800 last:border-0">
                    <span className="text-sm text-zinc-300">{FLAG_LABELS[key] || key}</span>
                    <input
                      type="checkbox"
                      checked={flags[key] === true}
                      onChange={(e) => setFlags((f) => ({ ...f, [key]: e.target.checked }))}
                      className="h-4 w-4 rounded border-zinc-600 bg-zinc-900 text-indigo-500"
                    />
                  </label>
                ))}
              </div>
            </Card>

            {members.length > 0 && (
              <Card className="p-6 space-y-3">
                <h2 className="text-lg font-semibold text-zinc-100">Members</h2>
                {members.map((m) => (
                  <div key={m.userId} className="flex items-center justify-between text-sm py-2 border-b border-zinc-800 last:border-0">
                    <div>
                      <p className="text-zinc-200">{m.firstName} {m.lastName}</p>
                      <p className="text-zinc-500 text-xs">{m.email}</p>
                    </div>
                    <span className="text-xs text-zinc-400">{m.roleName}</span>
                  </div>
                ))}
              </Card>
            )}

            <Button onClick={save} disabled={saving}>{saving ? "Saving..." : "Save changes"}</Button>
          </>
        )}
      </div>
    </AppLayout>
  );
}
