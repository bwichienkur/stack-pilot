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
import { useApprovalGates, useUpdateApprovalGates, type ApprovalGate, type OrganizationInvite, type OrganizationInviteCreated, type InvitableRole, type OutboundWebhook, type SamlConfig } from "@/lib/api-hooks";
import { Trash2, UserPlus } from "lucide-react";

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
    samlSso?: boolean;
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
  const [gates, setGates] = useState<ApprovalGate[]>([]);
  const [pendingInvites, setPendingInvites] = useState<OrganizationInvite[]>([]);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRoleId, setInviteRoleId] = useState("");
  const [invitableRoles, setInvitableRoles] = useState<InvitableRole[]>([]);
  const [lastInviteUrl, setLastInviteUrl] = useState<string | null>(null);
  const [webhooks, setWebhooks] = useState<OutboundWebhook[]>([]);
  const [webhookUrl, setWebhookUrl] = useState("");
  const [webhookEvents, setWebhookEvents] = useState("ticket.approved,ticket.released");
  const [samlConfig, setSamlConfig] = useState<SamlConfig>({ enabled: false });
  const [samlSaving, setSamlSaving] = useState(false);
  const [gdprLoading, setGdprLoading] = useState(false);

  const { data: approvalGates } = useApprovalGates();
  const updateGates = useUpdateApprovalGates();

  useEffect(() => {
    if (approvalGates) setGates(approvalGates);
  }, [approvalGates]);

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
      const [s, m, b, invites, hooks, saml, roles] = await Promise.all([
        api<Settings>(`/organizations/${orgId}/settings`, {}, token, orgId),
        api<Member[]>(`/organizations/${orgId}/members`, {}, token, orgId).catch(() => []),
        api<OrganizationBilling>(`/billing/organizations/${orgId}`, {}, token, orgId).catch(() => null),
        api<OrganizationInvite[]>(`/organizations/${orgId}/invites`, {}, token, orgId).catch(() => []),
        api<OutboundWebhook[]>(`/organizations/${orgId}/webhooks`, {}, token, orgId).catch(() => []),
        api<SamlConfig>(`/organizations/${orgId}/saml`, {}, token, orgId).catch(() => ({ enabled: false })),
        api<InvitableRole[]>(`/organizations/${orgId}/invitable-roles`, {}, token, orgId).catch(() => []),
      ]);
      setSettings(s);
      setBilling(b);
      setName(s.name);
      setFlags(s.featureFlags || {});
      setSlackWebhook(s.slackWebhookUrl || "");
      setMembers(m);
      setPendingInvites(invites);
      setWebhooks(hooks);
      setSamlConfig(saml);
      setInvitableRoles(roles);
      if (roles.length > 0 && !inviteRoleId) {
        const developer = roles.find((r) => r.name === "Developer") ?? roles[0];
        setInviteRoleId(developer.id);
      }
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load settings", "error");
    } finally {
      setLoading(false);
    }
  };

  const saveApprovalGates = async () => {
    try {
      const updated = await updateGates.mutateAsync(
        gates.map((g) => ({ id: g.id, isEnabled: g.isEnabled, sortOrder: g.sortOrder }))
      );
      setGates(updated);
      showToast("Approval gates saved", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to save approval gates", "error");
    }
  };

  const sendInvite = async () => {
    if (!inviteEmail.trim() || !inviteRoleId) return;
    try {
      const created = await api<OrganizationInviteCreated>(`/organizations/${orgId}/invites`, {
        method: "POST",
        body: JSON.stringify({ email: inviteEmail.trim(), roleId: inviteRoleId }),
      }, token, orgId);
      setLastInviteUrl(created.inviteUrl);
      showToast("Invite created — share the link with your teammate", "success");
      setInviteEmail("");
      await load();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to send invite", "error");
    }
  };

  const copyInviteLink = async () => {
    if (!lastInviteUrl || typeof navigator === "undefined") return;
    try {
      await navigator.clipboard.writeText(lastInviteUrl);
      showToast("Invite link copied", "success");
    } catch {
      showToast("Could not copy link", "error");
    }
  };

  const exportOrganizationData = async () => {
    if (!orgId || !token) return;
    setGdprLoading(true);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api/v1"}/organizations/${orgId}/export`, {
        method: "POST",
        headers: { Authorization: `Bearer ${token}`, "X-Organization-Id": orgId },
      });
      if (!res.ok) throw new Error("Export failed");
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `stackpilot-export-${orgId}.json`;
      a.click();
      URL.revokeObjectURL(url);
      showToast("Data export downloaded", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Export failed", "error");
    } finally {
      setGdprLoading(false);
    }
  };

  const deleteOrganizationData = async () => {
    if (!orgId || !token) return;
    if (!window.confirm("Permanently erase all organization data? This cannot be undone.")) return;
    setGdprLoading(true);
    try {
      await api(`/organizations/${orgId}/delete-data`, { method: "POST" }, token, orgId);
      showToast("Organization data erased", "success");
      router.push("/login");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Delete failed", "error");
    } finally {
      setGdprLoading(false);
    }
  };

  const revokeInvite = async (inviteId: string) => {
    try {
      await api(`/organizations/${orgId}/invites/${inviteId}`, { method: "DELETE" }, token, orgId);
      showToast("Invite revoked", "success");
      setPendingInvites((prev) => prev.filter((i) => i.id !== inviteId));
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to revoke invite", "error");
    }
  };

  const createWebhook = async () => {
    if (!webhookUrl.trim()) return;
    try {
      const created = await api<OutboundWebhook>(`/organizations/${orgId}/webhooks`, {
        method: "POST",
        body: JSON.stringify({
          url: webhookUrl.trim(),
          events: webhookEvents.split(",").map((e) => e.trim()).filter(Boolean),
        }),
      }, token, orgId);
      setWebhooks((prev) => [...prev, created]);
      setWebhookUrl("");
      showToast("Webhook created", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to create webhook", "error");
    }
  };

  const deleteWebhook = async (webhookId: string) => {
    try {
      await api(`/organizations/${orgId}/webhooks/${webhookId}`, { method: "DELETE" }, token, orgId);
      setWebhooks((prev) => prev.filter((w) => w.id !== webhookId));
      showToast("Webhook deleted", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to delete webhook", "error");
    }
  };

  const toggleWebhook = async (webhook: OutboundWebhook) => {
    try {
      const updated = await api<OutboundWebhook>(`/organizations/${orgId}/webhooks/${webhook.id}`, {
        method: "PUT",
        body: JSON.stringify({ url: webhook.url, events: webhook.events, isActive: !webhook.isActive }),
      }, token, orgId);
      setWebhooks((prev) => prev.map((w) => (w.id === webhook.id ? updated : w)));
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to update webhook", "error");
    }
  };

  const saveSaml = async () => {
    setSamlSaving(true);
    try {
      const updated = await api<SamlConfig>(`/organizations/${orgId}/saml`, {
        method: "PUT",
        body: JSON.stringify(samlConfig),
      }, token, orgId);
      setSamlConfig(updated);
      showToast("SAML configuration saved", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to save SAML config", "error");
    } finally {
      setSamlSaving(false);
    }
  };

  const isProfessionalPlan = billing?.plan === "Professional" || billing?.plan === "Enterprise" || billing?.limits?.samlSso;

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
              <p className="text-sm text-zinc-400">Toggle optional workflow modules in the sidebar.</p>
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
              <h2 className="text-lg font-semibold text-zinc-100">Approval Gates</h2>
              <p className="text-sm text-zinc-400">Configure which approval gates are required before implementation.</p>
              {gates.length === 0 ? (
                <p className="text-sm text-zinc-500">No approval gates configured.</p>
              ) : (
                <div className="space-y-3">
                  {gates.map((gate) => (
                    <label key={gate.id} className="flex items-center justify-between gap-4 py-2 border-b border-zinc-800 last:border-0">
                      <div>
                        <span className="text-sm text-zinc-300">{gate.gateType}</span>
                        <p className="text-xs text-zinc-500">{gate.requiredPermission}</p>
                      </div>
                      <input
                        type="checkbox"
                        checked={gate.isEnabled}
                        onChange={(e) => setGates((prev) => prev.map((g) => g.id === gate.id ? { ...g, isEnabled: e.target.checked } : g))}
                        className="h-4 w-4 rounded border-zinc-600 bg-zinc-900 text-indigo-500"
                      />
                    </label>
                  ))}
                </div>
              )}
              <Button size="sm" variant="secondary" onClick={saveApprovalGates} disabled={updateGates.isPending}>
                {updateGates.isPending ? "Saving..." : "Save approval gates"}
              </Button>
            </Card>

            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Member Invites</h2>
              <div className="flex flex-col sm:flex-row gap-2">
                <Input
                  value={inviteEmail}
                  onChange={(e) => setInviteEmail(e.target.value)}
                  placeholder="colleague@company.com"
                  type="email"
                />
                <select
                  value={inviteRoleId}
                  onChange={(e) => setInviteRoleId(e.target.value)}
                  className="h-10 rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100"
                >
                  {invitableRoles.map((r) => (
                    <option key={r.id} value={r.id}>{r.name}</option>
                  ))}
                </select>
                <Button onClick={sendInvite}><UserPlus className="h-4 w-4" /> Invite</Button>
              </div>
              {lastInviteUrl && (
                <div className="rounded-lg border border-zinc-700 bg-zinc-900/50 p-3 space-y-2">
                  <p className="text-xs text-zinc-500">Share this invite link (valid 7 days):</p>
                  <p className="text-sm text-zinc-300 break-all font-mono">{lastInviteUrl}</p>
                  <Button size="sm" variant="secondary" onClick={copyInviteLink}>Copy link</Button>
                </div>
              )}
              {pendingInvites.length > 0 && (
                <div className="space-y-2 pt-2">
                  <p className="text-xs text-zinc-500 uppercase tracking-wide">Pending invites</p>
                  {pendingInvites.map((inv) => (
                    <div key={inv.id} className="flex items-center justify-between text-sm py-2 border-b border-zinc-800 last:border-0">
                      <div>
                        <p className="text-zinc-200">{inv.email}</p>
                        <p className="text-xs text-zinc-500">{inv.roleName} · expires {new Date(inv.expiresAt).toLocaleDateString()}</p>
                      </div>
                      <Button size="sm" variant="ghost" onClick={() => revokeInvite(inv.id)}>
                        <Trash2 className="h-4 w-4 text-red-400" />
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </Card>

            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Outbound Webhooks</h2>
              <p className="text-sm text-zinc-400">Receive HTTP callbacks when key events occur in your organization.</p>
              <Input value={webhookUrl} onChange={(e) => setWebhookUrl(e.target.value)} placeholder="https://your-app.com/webhooks/stackpilot" />
              <Input value={webhookEvents} onChange={(e) => setWebhookEvents(e.target.value)} placeholder="ticket.approved,ticket.released" />
              <Button size="sm" onClick={createWebhook}>Add webhook</Button>
              {webhooks.length > 0 && (
                <div className="space-y-2 pt-2">
                  {webhooks.map((wh) => (
                    <div key={wh.id} className="flex items-center justify-between gap-4 py-2 border-b border-zinc-800 last:border-0 text-sm">
                      <div className="min-w-0">
                        <p className="text-zinc-200 truncate">{wh.url}</p>
                        <p className="text-xs text-zinc-500">{wh.events.join(", ")}</p>
                      </div>
                      <div className="flex gap-2 flex-shrink-0">
                        <Button size="sm" variant="secondary" onClick={() => toggleWebhook(wh)}>
                          {wh.isActive ? "Active" : "Paused"}
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => deleteWebhook(wh.id)}>
                          <Trash2 className="h-4 w-4 text-red-400" />
                        </Button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </Card>

            {isProfessionalPlan ? (
              <Card className="p-6 space-y-4">
                <h2 className="text-lg font-semibold text-zinc-100">SAML SSO</h2>
                <p className="text-sm text-zinc-400">Configure SAML 2.0 single sign-on for your identity provider.</p>
                <label className="flex items-center gap-3 text-sm text-zinc-300">
                  <input
                    type="checkbox"
                    checked={samlConfig.enabled}
                    onChange={(e) => setSamlConfig((c) => ({ ...c, enabled: e.target.checked }))}
                    className="h-4 w-4 rounded border-zinc-600 bg-zinc-900 text-indigo-500"
                  />
                  Enable SAML SSO
                </label>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">Entity ID</label>
                  <Input value={samlConfig.entityId || ""} onChange={(e) => setSamlConfig((c) => ({ ...c, entityId: e.target.value }))} />
                </div>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">IdP Metadata URL</label>
                  <Input value={samlConfig.idpMetadataUrl || ""} onChange={(e) => setSamlConfig((c) => ({ ...c, idpMetadataUrl: e.target.value }))} />
                </div>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">IdP SSO URL (recommended for production)</label>
                  <Input value={samlConfig.idpSsoUrl || ""} onChange={(e) => setSamlConfig((c) => ({ ...c, idpSsoUrl: e.target.value }))} placeholder="https://idp.example.com/saml/sso" />
                </div>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">IdP Certificate (PEM)</label>
                  <textarea
                    value={samlConfig.idpCertificate || ""}
                    onChange={(e) => setSamlConfig((c) => ({ ...c, idpCertificate: e.target.value }))}
                    rows={4}
                    className="flex w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 font-mono"
                  />
                </div>
                {samlConfig.metadataUrl && (
                  <p className="text-xs text-zinc-500">SP metadata: {samlConfig.metadataUrl}</p>
                )}
                {samlConfig.productionReady && (
                  <p className="text-xs text-emerald-400">Production-ready: certificate and SSO URL configured</p>
                )}
                <Button size="sm" onClick={saveSaml} disabled={samlSaving}>
                  {samlSaving ? "Saving..." : "Save SAML config"}
                </Button>
              </Card>
            ) : (
              <Card className="p-6 space-y-2">
                <h2 className="text-lg font-semibold text-zinc-100">SAML SSO</h2>
                <p className="text-sm text-zinc-400">
                  SAML single sign-on is available on Professional and Enterprise plans.{" "}
                  <Link href="/pricing" className="text-indigo-400 hover:text-indigo-300">View pricing</Link>
                </p>
              </Card>
            )}

            <Card className="p-6 space-y-4">
              <h2 className="text-lg font-semibold text-zinc-100">Data & privacy (GDPR)</h2>
              <p className="text-sm text-zinc-400">
                Export a JSON archive of organization data or permanently erase all tenant data. Erasure removes tickets, connectors, graph data, and audit logs.
              </p>
              <div className="flex flex-wrap gap-2">
                <Button size="sm" variant="secondary" disabled={gdprLoading} onClick={exportOrganizationData}>
                  Export organization data
                </Button>
                <Button size="sm" variant="destructive" disabled={gdprLoading} onClick={deleteOrganizationData}>
                  Erase organization data
                </Button>
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
