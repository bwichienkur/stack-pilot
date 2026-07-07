"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
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
  slackWebhookUrl?: string;
}

interface Member {
  userId: string;
  email: string;
  firstName?: string;
  lastName?: string;
  roleName: string;
  joinedAt: string;
}

interface OrganizationBilling {
  plan: string;
  subscriptionStatus: string;
  trialEndsAt?: string;
  trialDaysRemaining?: number;
  limits: {
    includedSeats: number;
    maxSeats: number;
    maxWorkspaces: number;
    maxConnectors: number;
    monthlyAiTokenBudget: number;
  };
  usage: {
    seatCount: number;
    workspaceCount: number;
    connectorCount: number;
    aiTokensUsedThisMonth: number;
  };
  stripeConfigured: boolean;
  isWriteBlocked: boolean;
  blockReason?: string;
  canOpenCustomerPortal: boolean;
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
  const [billing, setBilling] = useState<OrganizationBilling | null>(null);
  const [members, setMembers] = useState<Member[]>([]);
  const [name, setName] = useState("");
  const [flags, setFlags] = useState<Record<string, boolean>>({});
  const [slackWebhook, setSlackWebhook] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [checkoutLoading, setCheckoutLoading] = useState(false);
  const [portalLoading, setPortalLoading] = useState(false);
  const [promoCode, setPromoCode] = useState("DESIGNPARTNER20");

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (orgId) load();
  }, [token, orgId]);

  useEffect(() => {
    if (!billing || !token || !orgId || typeof window === "undefined") return;
    const upgrade = new URLSearchParams(window.location.search).get("upgrade");
    if (!upgrade) return;
    const plan = upgrade.charAt(0).toUpperCase() + upgrade.slice(1);
    window.history.replaceState(null, "", "/settings");
    void startCheckout(plan);
  }, [billing, token, orgId]);

  const load = async () => {
    try {
      const [s, m, b] = await Promise.all([
        api<Settings>(`/organizations/${orgId}/settings`, {}, token, orgId),
        api<Member[]>(`/organizations/${orgId}/members`, {}, token, orgId).catch(() => []),
        api<OrganizationBilling>(`/billing/organizations/${orgId}`, {}, token, orgId).catch(() => null),
      ]);
      setSettings(s);
      setBilling(b);
      setName(s.name);
      setFlags(s.featureFlags || {});
      setSlackWebhook(s.slackWebhookUrl || "");
      setMembers(m);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load settings", "error");
    } finally {
      setLoading(false);
    }
  };

  const startCheckout = async (plan: string) => {
    if (!orgId || plan === "Trial" || plan === "Enterprise") return;
    setCheckoutLoading(true);
    try {
      const origin = typeof window !== "undefined" ? window.location.origin : "";
      const session = await api<{ url: string; isMock: boolean }>(
        `/billing/organizations/${orgId}/checkout`,
        {
          method: "POST",
          body: JSON.stringify({
            plan,
            billingInterval: "monthly",
            successUrl: `${origin}/settings?checkout=success`,
            cancelUrl: `${origin}/pricing`,
            promotionCode: promoCode.trim() || undefined,
          }),
        },
        token,
        orgId
      );
      if (session.isMock) {
        showToast("Stripe not configured — mock checkout complete for demo", "success");
        await load();
      } else {
        window.location.href = session.url;
      }
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Checkout failed", "error");
    } finally {
      setCheckoutLoading(false);
    }
  };

  const openCustomerPortal = async () => {
    if (!orgId) return;
    setPortalLoading(true);
    try {
      const origin = typeof window !== "undefined" ? window.location.origin : "";
      const session = await api<{ url: string; isMock: boolean }>(
        `/billing/organizations/${orgId}/portal`,
        {
          method: "POST",
          body: JSON.stringify({ returnUrl: `${origin}/settings` }),
        },
        token,
        orgId
      );
      if (session.isMock) {
        showToast("Stripe not configured — customer portal unavailable in demo mode", "info");
      } else {
        window.location.href = session.url;
      }
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Could not open billing portal", "error");
    } finally {
      setPortalLoading(false);
    }
  };

  const save = async () => {
    setSaving(true);
    try {
      const updated = await api<Settings>(`/organizations/${orgId}/settings`, {
        method: "PUT",
        body: JSON.stringify({ name, featureFlags: flags, slackWebhookUrl: slackWebhook }),
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
              <div className="flex items-center justify-between gap-4">
                <h2 className="text-lg font-semibold text-zinc-100">Billing</h2>
                <Link href="/pricing" className="text-sm text-indigo-400 hover:text-indigo-300">View all plans</Link>
              </div>
              {billing ? (
                <>
                  {billing.isWriteBlocked && billing.blockReason && (
                    <div className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-200">
                      {billing.blockReason}
                    </div>
                  )}
                  {!billing.isWriteBlocked && billing.usage.connectorCount >= billing.limits.maxConnectors && (
                    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 px-4 py-3 text-sm text-zinc-300">
                      Connector limit reached. Upgrade your plan to add more integrations.
                    </div>
                  )}
                  <div className="grid grid-cols-2 gap-4 text-sm">
                    <div>
                      <p className="text-zinc-500">Status</p>
                      <p className="text-zinc-200">{billing.subscriptionStatus}</p>
                    </div>
                    {billing.trialDaysRemaining != null && billing.plan === "Trial" && (
                      <div>
                        <p className="text-zinc-500">Trial remaining</p>
                        <p className="text-zinc-200">{billing.trialDaysRemaining} days</p>
                      </div>
                    )}
                    <div>
                      <p className="text-zinc-500">Seats</p>
                      <p className="text-zinc-200">{billing.usage.seatCount} / {billing.limits.maxSeats}</p>
                    </div>
                    <div>
                      <p className="text-zinc-500">Connectors</p>
                      <p className="text-zinc-200">{billing.usage.connectorCount} / {billing.limits.maxConnectors}</p>
                    </div>
                    <div className="col-span-2">
                      <p className="text-zinc-500">AI tokens this month</p>
                      <p className="text-zinc-200">
                        {billing.usage.aiTokensUsedThisMonth.toLocaleString()} / {billing.limits.monthlyAiTokenBudget.toLocaleString()}
                      </p>
                    </div>
                  </div>
                  {(billing.plan === "Trial" || billing.isWriteBlocked) && (
                    <div className="space-y-3 pt-2 border-t border-zinc-800">
                      <div>
                        <label className="text-sm text-zinc-400 mb-1 block">Promotion code</label>
                        <Input
                          value={promoCode}
                          onChange={(e) => setPromoCode(e.target.value)}
                          placeholder="DESIGNPARTNER20"
                        />
                        <p className="text-xs text-zinc-500 mt-1">Design partners: 20% off year one with DESIGNPARTNER20</p>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button size="sm" disabled={checkoutLoading} onClick={() => startCheckout("Starter")}>Upgrade to Starter</Button>
                        <Button size="sm" variant="secondary" disabled={checkoutLoading} onClick={() => startCheckout("Professional")}>Upgrade to Professional</Button>
                      </div>
                    </div>
                  )}
                  {billing.canOpenCustomerPortal && billing.plan !== "Trial" && (
                    <div className="pt-2">
                      <Button size="sm" variant="secondary" disabled={portalLoading} onClick={openCustomerPortal}>
                        {portalLoading ? "Opening portal..." : "Manage billing"}
                      </Button>
                    </div>
                  )}
                </>
              ) : (
                <p className="text-sm text-zinc-500">Billing details unavailable.</p>
              )}
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

            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Notifications</h2>
              <div>
                <label className="text-sm text-zinc-400 mb-1 block">Slack webhook URL</label>
                <Input
                  value={slackWebhook}
                  onChange={(e) => setSlackWebhook(e.target.value)}
                  placeholder="https://hooks.slack.com/services/..."
                />
                <p className="text-xs text-zinc-500 mt-2">Ticket approvals and key events will post to this channel.</p>
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
