"use client";

import { useEffect, useState, type ReactNode } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Bot } from "lucide-react";
import { Button, Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";

export default function AcceptInviteForm() {
  const { token, setOrg } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const inviteToken = searchParams.get("token");

  const [status, setStatus] = useState<"idle" | "loading" | "success" | "error">("idle");
  const [message, setMessage] = useState("");
  const [orgName, setOrgName] = useState("");

  useEffect(() => {
    if (!inviteToken) {
      setStatus("error");
      setMessage("This invite link is missing a token.");
    }
  }, [inviteToken]);

  const acceptInvite = async () => {
    if (!inviteToken || !token) return;
    setStatus("loading");
    try {
      const org = await api<{ id: string; name: string }>(
        "/organizations/invites/accept",
        { method: "POST", body: JSON.stringify({ token: inviteToken }) },
        token
      );
      setOrg(org.id);
      setOrgName(org.name);
      setStatus("success");
      setTimeout(() => router.push("/"), 1500);
    } catch (err) {
      setStatus("error");
      setMessage(err instanceof Error ? err.message : "Could not accept invite");
    }
  };

  if (!inviteToken) {
    return (
      <InviteShell>
        <p className="text-sm text-red-400">{message || "Invalid invite link."}</p>
        <Link href="/login"><Button className="w-full mt-4">Go to sign in</Button></Link>
      </InviteShell>
    );
  }

  if (!token) {
    const returnUrl = `/invite/accept?token=${encodeURIComponent(inviteToken)}`;
    return (
      <InviteShell>
        <p className="text-sm text-zinc-400">
          Sign in or create an account with the email address that received this invite, then return here to join the organization.
        </p>
        <Link href={`/login?returnUrl=${encodeURIComponent(returnUrl)}`}>
          <Button className="w-full mt-4">Sign in to accept</Button>
        </Link>
      </InviteShell>
    );
  }

  return (
    <InviteShell>
      {status === "success" ? (
        <p className="text-sm text-emerald-400">You joined {orgName}. Redirecting to dashboard…</p>
      ) : (
        <>
          <p className="text-sm text-zinc-400">
            You are signed in. Accept this invitation to join the organization.
          </p>
          {status === "error" && <p className="text-sm text-red-400">{message}</p>}
          <Button className="w-full mt-4" onClick={acceptInvite} disabled={status === "loading"}>
            {status === "loading" ? "Accepting…" : "Accept invitation"}
          </Button>
        </>
      )}
    </InviteShell>
  );
}

function InviteShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-zinc-950 relative overflow-hidden">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-indigo-900/20 via-zinc-950 to-zinc-950" />
      <Card className="w-full max-w-md relative z-10 border-zinc-800/50 shadow-2xl">
        <CardHeader className="text-center">
          <div className="mx-auto h-14 w-14 rounded-2xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center mb-4 shadow-lg shadow-indigo-500/25">
            <Bot className="h-8 w-8 text-white" />
          </div>
          <CardTitle className="text-2xl">Organization invite</CardTitle>
          <CardDescription>Join your team on StackPilot</CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
    </div>
  );
}
