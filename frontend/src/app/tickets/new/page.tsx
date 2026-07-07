"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, Button, Input, Textarea } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";

export default function NewTicketPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [ticketType, setTicketType] = useState("Enhancement");
  const [priority, setPriority] = useState("Medium");
  const [justification, setJustification] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!token) router.push("/login");
  }, [token]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!workspaceId) return;
    setLoading(true);
    try {
      const ticket = await api<{ id: string }>(
        `/workspaces/${workspaceId}/tickets`,
        { method: "POST", body: JSON.stringify({ title, description, ticketType, priority, businessJustification: justification }) },
        token, orgId, workspaceId
      );
      router.push(`/tickets/${ticket.id}`);
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to create ticket", "error");
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="max-w-2xl mx-auto space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">Create Ticket</h1>
          <p className="text-zinc-400 mt-1">Submit a change request for AI analysis</p>
        </div>

        <Card>
          <CardContent className="p-6">
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="text-sm text-zinc-400 mb-1 block">Title</label>
                <Input value={title} onChange={(e) => setTitle(e.target.value)} required placeholder="Brief description of the change" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">Type</label>
                  <select value={ticketType} onChange={(e) => setTicketType(e.target.value)} className="flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100">
                    {["Bug", "Enhancement", "NewFeature", "Refactor", "DatabaseChange", "SecurityFix"].map((t) => (
                      <option key={t} value={t}>{t}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">Priority</label>
                  <select value={priority} onChange={(e) => setPriority(e.target.value)} className="flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100">
                    {["Low", "Medium", "High", "Critical"].map((p) => (
                      <option key={p} value={p}>{p}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div>
                <label className="text-sm text-zinc-400 mb-1 block">Description</label>
                <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} placeholder="Detailed description of the requested change" />
              </div>
              <div>
                <label className="text-sm text-zinc-400 mb-1 block">Business Justification</label>
                <Textarea value={justification} onChange={(e) => setJustification(e.target.value)} rows={3} placeholder="Why is this change needed?" />
              </div>
              <Button type="submit" disabled={loading} className="w-full">
                {loading ? "Submitting..." : "Submit for AI Analysis"}
              </Button>
            </form>
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}
