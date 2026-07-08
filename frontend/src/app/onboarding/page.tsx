"use client";

import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Button, Input, Card, CardContent } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";

const steps = ["Organization", "Workspace", "Invite team", "Connect"];

export default function OnboardingPage() {
  const { token, user, setAuth, setOrg, setWorkspace } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [step, setStep] = useState(0);
  const [orgName, setOrgName] = useState("My Organization");
  const [orgSlug, setOrgSlug] = useState(`org-${Date.now()}`);
  const [wsName, setWsName] = useState("Default");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token, router]);

  if (!token) {
    return null;
  }

  const createOrg = async () => {
    setLoading(true);
    try {
      const result = await api<{ organization: { id: string }; accessToken: string }>("/organizations", {
        method: "POST",
        body: JSON.stringify({ name: orgName, slug: orgSlug }),
      }, token);
      if (user) setAuth(result.accessToken, user);
      setOrg(result.organization.id);
      setStep(1);
      showToast("Organization created", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to create organization", "error");
    } finally {
      setLoading(false);
    }
  };

  const createWorkspace = async () => {
    setLoading(true);
    try {
      const orgs = await api<{ id: string }[]>("/organizations", {}, token);
      const orgId = orgs[0]?.id;
      if (!orgId) throw new Error("No organization found");
      const ws = await api<{ id: string }>(`/organizations/${orgId}/workspaces`, {
        method: "POST",
        body: JSON.stringify({ name: wsName, slug: wsName.toLowerCase().replace(/\s+/g, "-") }),
      }, token, orgId);
      setWorkspace(ws.id);
      setStep(2);
      showToast("Workspace ready", "success");
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to create workspace", "error");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-zinc-950 flex items-center justify-center p-6">
      <Card className="w-full max-w-lg">
        <CardContent className="p-8 space-y-6">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Welcome to StackPilot</h1>
            <p className="text-zinc-400 mt-1">Step {step + 1} of {steps.length}: {steps[step]}</p>
          </div>

          {step === 0 && (
            <div className="space-y-4">
              <Input placeholder="Organization name" value={orgName} onChange={(e) => setOrgName(e.target.value)} />
              <Input placeholder="Slug" value={orgSlug} onChange={(e) => setOrgSlug(e.target.value)} />
              <Button className="w-full" onClick={createOrg} disabled={loading}>Create Organization</Button>
            </div>
          )}

          {step === 1 && (
            <div className="space-y-4">
              <Input placeholder="Workspace name" value={wsName} onChange={(e) => setWsName(e.target.value)} />
              <Button className="w-full" onClick={createWorkspace} disabled={loading}>Create Workspace</Button>
            </div>
          )}

          {step === 2 && (
            <div className="space-y-4 text-center">
              <p className="text-zinc-400">Invite teammates from Settings to collaborate on approvals and releases.</p>
              <Button className="w-full" onClick={() => router.push("/settings")}>Open team invites</Button>
              <Button variant="secondary" className="w-full" onClick={() => setStep(3)}>Skip for now</Button>
            </div>
          )}

          {step === 3 && (
            <div className="space-y-4 text-center">
              <p className="text-zinc-400">Connect your first integration to populate your architecture graph.</p>
              <Button className="w-full" onClick={() => router.push("/connectors")}>Add connector</Button>
              <Button variant="secondary" className="w-full" onClick={() => router.push("/")}>Go to dashboard</Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
