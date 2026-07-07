"use client";

import { useEffect, useState } from "react";
import { useRouter, useParams } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, CardHeader, CardTitle, Badge, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { Bot, CheckCircle, XCircle } from "lucide-react";

interface TicketDetail {
  id: string;
  ticketNumber: number;
  title: string;
  description?: string;
  ticketType: string;
  status: string;
  priority: string;
  businessJustification?: string;
  aiRequirementsJson?: string;
  implementationPlanJson?: string;
  riskScore?: number;
  confidenceScore?: number;
  comments: { id: string; content: string; createdAt: string }[];
  approvals: { id: string; approvalType: string; decision: string; comments?: string }[];
}

export default function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { token, orgId } = useAuth();
  const router = useRouter();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [activeTab, setActiveTab] = useState("details");

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    loadTicket();
  }, [token, id]);

  const loadTicket = async () => {
    try {
      const data = await api<TicketDetail>(`/tickets/${id}`, {}, token, orgId);
      setTicket(data);
    } catch (err) {
      console.error(err);
    }
  };

  const generateRequirements = async () => {
    await api(`/tickets/${id}/generate-requirements`, { method: "POST" }, token, orgId);
    loadTicket();
  };

  const generatePlan = async () => {
    await api(`/tickets/${id}/generate-plan`, { method: "POST" }, token, orgId);
    loadTicket();
  };

  const submitApproval = async (decision: string) => {
    await api(`/tickets/${id}/approvals`, {
      method: "POST",
      body: JSON.stringify({ approvalType: "TechnicalReviewer", decision, comments: "" })
    }, token, orgId);
    loadTicket();
  };

  if (!token || !ticket) return null;

  const tabs = ["details", "requirements", "plan", "approvals", "activity"];

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-start justify-between">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <span className="text-zinc-500">#{ticket.ticketNumber}</span>
              <Badge>{ticket.status}</Badge>
              <Badge variant="warning">{ticket.priority}</Badge>
            </div>
            <h1 className="text-2xl font-bold text-zinc-100">{ticket.title}</h1>
            <p className="text-zinc-400 mt-1">{ticket.ticketType}</p>
          </div>
          <div className="flex gap-2">
            {!ticket.aiRequirementsJson && (
              <Button onClick={generateRequirements}><Bot className="h-4 w-4" /> Generate Requirements</Button>
            )}
            {ticket.aiRequirementsJson && !ticket.implementationPlanJson && (
              <Button onClick={generatePlan}><Bot className="h-4 w-4" /> Generate Plan</Button>
            )}
            {ticket.status === "AwaitingApproval" && (
              <>
                <Button variant="secondary" onClick={() => submitApproval("Approved")}><CheckCircle className="h-4 w-4" /> Approve</Button>
                <Button variant="destructive" onClick={() => submitApproval("Rejected")}><XCircle className="h-4 w-4" /> Reject</Button>
              </>
            )}
          </div>
        </div>

        {(ticket.riskScore !== undefined || ticket.confidenceScore !== undefined) && (
          <div className="flex gap-4">
            {ticket.riskScore !== undefined && (
              <Card className="px-6 py-4"><span className="text-sm text-zinc-400">Risk Score</span><p className="text-2xl font-bold text-amber-400">{ticket.riskScore}</p></Card>
            )}
            {ticket.confidenceScore !== undefined && (
              <Card className="px-6 py-4"><span className="text-sm text-zinc-400">AI Confidence</span><p className="text-2xl font-bold text-indigo-400">{(ticket.confidenceScore * 100).toFixed(0)}%</p></Card>
            )}
          </div>
        )}

        <div className="flex gap-1 border-b border-zinc-800">
          {tabs.map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-4 py-2 text-sm capitalize transition-colors ${activeTab === tab ? "text-indigo-400 border-b-2 border-indigo-400" : "text-zinc-400 hover:text-zinc-200"}`}
            >
              {tab}
            </button>
          ))}
        </div>

        {activeTab === "details" && (
          <Card>
            <CardContent className="p-6 space-y-4">
              <div><h3 className="text-sm text-zinc-400 mb-1">Description</h3><p className="text-zinc-200">{ticket.description || "No description"}</p></div>
              <div><h3 className="text-sm text-zinc-400 mb-1">Business Justification</h3><p className="text-zinc-200">{ticket.businessJustification || "Not provided"}</p></div>
            </CardContent>
          </Card>
        )}

        {activeTab === "requirements" && ticket.aiRequirementsJson && (
          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2"><Bot className="h-5 w-5 text-indigo-400" /> AI-Generated Requirements</CardTitle></CardHeader>
            <CardContent><pre className="text-sm text-zinc-300 whitespace-pre-wrap">{ticket.aiRequirementsJson}</pre></CardContent>
          </Card>
        )}

        {activeTab === "plan" && ticket.implementationPlanJson && (
          <Card>
            <CardHeader><CardTitle>Implementation Plan</CardTitle></CardHeader>
            <CardContent><pre className="text-sm text-zinc-300 whitespace-pre-wrap">{ticket.implementationPlanJson}</pre></CardContent>
          </Card>
        )}

        {activeTab === "approvals" && (
          <Card>
            <CardContent className="p-6">
              {ticket.approvals.length === 0 ? (
                <p className="text-zinc-400">No approvals yet</p>
              ) : (
                <div className="space-y-4">
                  {ticket.approvals.map((a) => (
                    <div key={a.id} className="flex items-center gap-4 py-3 border-b border-zinc-800 last:border-0">
                      <Badge variant={a.decision === "Approved" ? "success" : "danger"}>{a.decision}</Badge>
                      <span className="text-sm text-zinc-300">{a.approvalType}</span>
                      {a.comments && <span className="text-sm text-zinc-500">{a.comments}</span>}
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        )}
      </div>
    </AppLayout>
  );
}
