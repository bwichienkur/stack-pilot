"use client";

import { useEffect, useState } from "react";
import { useRouter, useParams } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, CardHeader, CardTitle, Badge, Button, Input } from "@/components/ui";
import { PageSkeleton } from "@/components/page-skeleton";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { useTicketWorkflowStates } from "@/lib/api-hooks";
import { getFallbackWorkflowStates } from "@/lib/ticket-workflow";
import { Bot, CheckCircle, XCircle, GitBranch, ExternalLink, Calendar, ChevronDown, ChevronUp, Target } from "lucide-react";

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
  createdAt: string;
  updatedAt: string;
  comments: { id: string; content: string; createdAt: string }[];
  approvals: { id: string; approvalType: string; decision: string; comments?: string; decidedAt?: string }[];
}

interface ParsedRequirements {
  businessSummary?: string;
  functionalRequirements?: string;
  nonFunctionalRequirements?: string;
  acceptanceCriteria?: string;
  citations?: { nodeId?: string; excerpt?: string }[];
}

interface BuildRun {
  id: string;
  ticketId?: string;
  status: string;
  conclusion?: string;
  logsUrl?: string;
  pullRequestUrl?: string;
  startedAt?: string;
  completedAt?: string;
}

interface ImpactNode {
  id: string;
  name: string;
  nodeType: string;
  riskScore?: number;
}

interface ImpactAnalysis {
  nodeId: string;
  impactedNodes: ImpactNode[];
  paths: { sourceNodeId: string; targetNodeId: string; edgeType: string }[];
}

export default function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [buildRuns, setBuildRuns] = useState<BuildRun[]>([]);
  const [impact, setImpact] = useState<ImpactAnalysis | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("details");
  const [showScheduleModal, setShowScheduleModal] = useState(false);
  const [scheduleDate, setScheduleDate] = useState("");
  const [releaseWindow, setReleaseWindow] = useState("Maintenance");
  const [scheduling, setScheduling] = useState(false);
  const [expandedNodes, setExpandedNodes] = useState<Set<string>>(new Set());
  const [sortByRisk, setSortByRisk] = useState(true);

  const { data: workflowData } = useTicketWorkflowStates(id);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    loadTicket();
  }, [token, id]);

  const loadTicket = async () => {
    try {
      const data = await api<TicketDetail>(`/tickets/${id}`, {}, token, orgId, workspaceId);
      setTicket(data);
      const runs = await api<BuildRun[]>(`/tickets/${id}/build-runs`, {}, token, orgId, workspaceId);
      setBuildRuns(runs);

      const parsed = parseRequirements(data.aiRequirementsJson);
      const citationNodeId = parsed?.citations?.find((c) => c.nodeId)?.nodeId;
      if (citationNodeId) {
        try {
          const impactData = await api<ImpactAnalysis>(`/graph/nodes/${citationNodeId}/impact`, { method: "POST" }, token, orgId, workspaceId);
          setImpact(impactData);
        } catch {
          setImpact(null);
        }
      }
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to load ticket", "error");
    } finally {
      setLoading(false);
    }
  };

  const parseRequirements = (json?: string): ParsedRequirements | null => {
    if (!json) return null;
    try { return JSON.parse(json); } catch { return null; }
  };

  const generateRequirements = async () => {
    try {
      await api(`/tickets/${id}/generate-requirements`, { method: "POST" }, token, orgId, workspaceId);
      showToast("Requirements generated", "success");
      loadTicket();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to generate requirements", "error");
    }
  };

  const generatePlan = async () => {
    try {
      await api(`/tickets/${id}/generate-plan`, { method: "POST" }, token, orgId, workspaceId);
      showToast("Implementation plan generated", "success");
      loadTicket();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to generate plan", "error");
    }
  };

  const submitApproval = async (decision: string) => {
    try {
      await api(`/tickets/${id}/approvals`, {
        method: "POST",
        body: JSON.stringify({ approvalType: "TechnicalReviewer", decision, comments: "" }),
      }, token, orgId, workspaceId);
      showToast(decision === "Approved" ? "Approved" : "Rejected", "success");
      loadTicket();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Approval failed", "error");
    }
  };

  const scheduleRelease = async () => {
    if (!scheduleDate) {
      showToast("Select a release date", "error");
      return;
    }
    setScheduling(true);
    try {
      await api(`/tickets/${id}/schedule-release`, {
        method: "POST",
        body: JSON.stringify({
          scheduledAt: new Date(scheduleDate).toISOString(),
          releaseWindow,
        }),
      }, token, orgId, workspaceId);
      showToast("Release scheduled", "success");
      setShowScheduleModal(false);
      loadTicket();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Failed to schedule release", "error");
    } finally {
      setScheduling(false);
    }
  };

  const toggleNodeExpand = (nodeId: string) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  };

  if (!token) return null;
  if (loading) return <AppLayout><PageSkeleton rows={4} /></AppLayout>;
  if (!ticket) return <AppLayout><p className="text-zinc-400">Ticket not found</p></AppLayout>;

  const parsed = parseRequirements(ticket.aiRequirementsJson);
  const planSections = ticket.implementationPlanJson?.includes("##")
    ? ticket.implementationPlanJson.split(/(?=^## )/m).filter(Boolean)
    : ticket.implementationPlanJson ? [ticket.implementationPlanJson] : [];

  const tabs = ["details", "requirements", "plan", "impact", "builds", "approvals", "activity"];
  const canApprove = ticket.status === "AwaitingApproval" || ticket.status === "RequirementsDrafted";
  const canScheduleRelease = ticket.status === "UatAccepted";
  const workflow = workflowData ?? getFallbackWorkflowStates(ticket.status);

  const sortedImpactNodes = impact?.impactedNodes
    ? [...impact.impactedNodes].sort((a, b) => sortByRisk ? (b.riskScore ?? 0) - (a.riskScore ?? 0) : a.name.localeCompare(b.name))
    : [];

  const highRiskCount = sortedImpactNodes.filter((n) => (n.riskScore ?? 0) >= 7).length;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-start justify-between flex-wrap gap-4">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <span className="text-zinc-500">#{ticket.ticketNumber}</span>
              <Badge>{ticket.status}</Badge>
              <Badge variant="warning">{ticket.priority}</Badge>
            </div>
            <h1 className="text-2xl font-bold text-zinc-100">{ticket.title}</h1>
            <p className="text-zinc-400 mt-1">{ticket.ticketType}</p>
            {workflow.allowedTransitions.length > 0 && (
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <span className="text-xs text-zinc-500">Allowed transitions:</span>
                {workflow.allowedTransitions.map((s) => (
                  <Badge key={s} variant="neutral">{s}</Badge>
                ))}
              </div>
            )}
          </div>
          <div className="flex gap-2 flex-wrap">
            {canScheduleRelease && (
              <Button onClick={() => setShowScheduleModal(true)}>
                <Calendar className="h-4 w-4" /> Schedule Release
              </Button>
            )}
            {!ticket.aiRequirementsJson && (
              <Button onClick={generateRequirements}><Bot className="h-4 w-4" /> Generate Requirements</Button>
            )}
            {ticket.aiRequirementsJson && !ticket.implementationPlanJson && ticket.status === "Approved" && (
              <Button onClick={generatePlan}><Bot className="h-4 w-4" /> Generate Plan</Button>
            )}
            {canApprove && (
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

        <div className="flex gap-1 border-b border-zinc-800 overflow-x-auto">
          {tabs.map((tab) => (
            <button
              key={tab}
              onClick={() => setActiveTab(tab)}
              className={`px-4 py-2 text-sm capitalize transition-colors whitespace-nowrap ${activeTab === tab ? "text-indigo-400 border-b-2 border-indigo-400" : "text-zinc-400 hover:text-zinc-200"}`}
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
            <CardContent className="space-y-4">
              {parsed ? (
                <>
                  {parsed.businessSummary && <div><h4 className="text-sm text-zinc-400">Business Summary</h4><p className="text-zinc-200 mt-1">{parsed.businessSummary}</p></div>}
                  {parsed.functionalRequirements && <div><h4 className="text-sm text-zinc-400">Functional</h4><p className="text-zinc-200 mt-1 whitespace-pre-wrap">{parsed.functionalRequirements}</p></div>}
                  {parsed.nonFunctionalRequirements && <div><h4 className="text-sm text-zinc-400">Non-Functional</h4><p className="text-zinc-200 mt-1 whitespace-pre-wrap">{parsed.nonFunctionalRequirements}</p></div>}
                  {parsed.acceptanceCriteria && <div><h4 className="text-sm text-zinc-400">Acceptance Criteria</h4><p className="text-zinc-200 mt-1 whitespace-pre-wrap">{parsed.acceptanceCriteria}</p></div>}
                  {parsed.citations && parsed.citations.length > 0 && (
                    <div>
                      <h4 className="text-sm text-zinc-400">Citations (graph-grounded)</h4>
                      <ul className="mt-2 space-y-1">{parsed.citations.map((c, i) => (
                        <li key={i} className="text-xs text-indigo-300">[{c.nodeId || "graph"}] {c.excerpt}</li>
                      ))}</ul>
                    </div>
                  )}
                </>
              ) : (
                <pre className="text-sm text-zinc-300 whitespace-pre-wrap">{ticket.aiRequirementsJson}</pre>
              )}
            </CardContent>
          </Card>
        )}

        {activeTab === "plan" && ticket.implementationPlanJson && (
          <Card>
            <CardHeader><CardTitle>Implementation Plan</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {planSections.map((section, i) => (
                <pre key={i} className="text-sm text-zinc-300 whitespace-pre-wrap">{section}</pre>
              ))}
            </CardContent>
          </Card>
        )}

        {activeTab === "impact" && (
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center justify-between gap-4 flex-wrap">
                <span className="flex items-center gap-2">
                  <Target className="h-5 w-5 text-amber-400" />
                  Blast Radius Analysis
                </span>
                {impact && impact.impactedNodes.length > 0 && (
                  <div className="flex gap-2">
                    <Button size="sm" variant={sortByRisk ? "default" : "secondary"} onClick={() => setSortByRisk(true)}>
                      Sort by risk
                    </Button>
                    <Button size="sm" variant={!sortByRisk ? "default" : "secondary"} onClick={() => setSortByRisk(false)}>
                      Sort by name
                    </Button>
                  </div>
                )}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {impact && sortedImpactNodes.length > 0 ? (
                <div className="space-y-4">
                  <div className="grid grid-cols-3 gap-3">
                    <div className="rounded-lg bg-zinc-800/50 p-3 text-center">
                      <p className="text-2xl font-bold text-zinc-100">{sortedImpactNodes.length}</p>
                      <p className="text-xs text-zinc-500">Impacted nodes</p>
                    </div>
                    <div className="rounded-lg bg-amber-500/10 p-3 text-center border border-amber-500/20">
                      <p className="text-2xl font-bold text-amber-400">{highRiskCount}</p>
                      <p className="text-xs text-zinc-500">High risk (≥7)</p>
                    </div>
                    <div className="rounded-lg bg-zinc-800/50 p-3 text-center">
                      <p className="text-2xl font-bold text-indigo-400">{impact.paths.length}</p>
                      <p className="text-xs text-zinc-500">Dependency paths</p>
                    </div>
                  </div>
                  <div className="space-y-2">
                    {sortedImpactNodes.map((n) => {
                      const expanded = expandedNodes.has(n.id);
                      const relatedPaths = impact.paths.filter(
                        (p) => p.sourceNodeId === n.id || p.targetNodeId === n.id
                      );
                      return (
                        <div key={n.id} className="rounded-lg border border-zinc-800 overflow-hidden">
                          <button
                            type="button"
                            onClick={() => toggleNodeExpand(n.id)}
                            className="w-full flex items-center justify-between gap-3 px-4 py-3 hover:bg-zinc-800/30 transition-colors text-left"
                          >
                            <div className="flex items-center gap-3 min-w-0">
                              {expanded ? <ChevronUp className="h-4 w-4 text-zinc-500 flex-shrink-0" /> : <ChevronDown className="h-4 w-4 text-zinc-500 flex-shrink-0" />}
                              <span className="text-zinc-200 truncate">{n.name}</span>
                            </div>
                            <div className="flex gap-2 flex-shrink-0">
                              <Badge variant="neutral">{n.nodeType}</Badge>
                              {n.riskScore !== undefined && (
                                <Badge variant={n.riskScore >= 7 ? "danger" : n.riskScore >= 4 ? "warning" : "success"}>
                                  Risk {n.riskScore}
                                </Badge>
                              )}
                            </div>
                          </button>
                          {expanded && relatedPaths.length > 0 && (
                            <div className="px-4 pb-3 pt-1 border-t border-zinc-800 bg-zinc-950/50">
                              <p className="text-xs text-zinc-500 mb-2">Connected paths</p>
                              <ul className="space-y-1">
                                {relatedPaths.map((p, i) => (
                                  <li key={i} className="text-xs text-zinc-400 font-mono">
                                    {p.edgeType}: {p.sourceNodeId.slice(0, 8)}… → {p.targetNodeId.slice(0, 8)}…
                                  </li>
                                ))}
                              </ul>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              ) : (
                <p className="text-zinc-400">Generate requirements with graph citations to see blast-radius analysis.</p>
              )}
            </CardContent>
          </Card>
        )}

        {activeTab === "builds" && (
          <Card>
            <CardHeader><CardTitle className="flex items-center gap-2"><GitBranch className="h-5 w-5" /> Build Runs &amp; PRs</CardTitle></CardHeader>
            <CardContent>
              {buildRuns.length === 0 ? (
                <p className="text-zinc-400">No linked build runs. PRs are linked via GitHub Actions webhooks when branch names include the ticket number.</p>
              ) : (
                <div className="space-y-3">
                  {buildRuns.map((b) => (
                    <div key={b.id} className="flex items-center justify-between py-3 border-b border-zinc-800">
                      <div>
                        <Badge variant={b.conclusion === "success" ? "success" : b.conclusion === "failure" ? "danger" : "neutral"}>{b.status}</Badge>
                        {b.conclusion && <span className="text-sm text-zinc-400 ml-2">{b.conclusion}</span>}
                      </div>
                      <div className="flex gap-2">
                        {b.pullRequestUrl && (
                          <a href={b.pullRequestUrl} target="_blank" rel="noopener noreferrer" className="text-indigo-400 text-sm flex items-center gap-1">
                            PR <ExternalLink className="h-3 w-3" />
                          </a>
                        )}
                        {b.logsUrl && (
                          <a href={b.logsUrl} target="_blank" rel="noopener noreferrer" className="text-zinc-400 text-sm flex items-center gap-1">
                            Logs <ExternalLink className="h-3 w-3" />
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
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

        {activeTab === "activity" && (
          <Card>
            <CardContent className="p-6 space-y-4">
              <div className="text-sm text-zinc-400">Created {new Date(ticket.createdAt).toLocaleString()}</div>
              <div className="text-sm text-zinc-400">Updated {new Date(ticket.updatedAt).toLocaleString()}</div>
              {ticket.comments.map((c) => (
                <div key={c.id} className="border-t border-zinc-800 pt-3">
                  <p className="text-zinc-200">{c.content}</p>
                  <p className="text-xs text-zinc-500 mt-1">{new Date(c.createdAt).toLocaleString()}</p>
                </div>
              ))}
              {ticket.approvals.map((a) => (
                <div key={a.id} className="border-t border-zinc-800 pt-3 text-sm text-zinc-300">
                  {a.approvalType} — {a.decision}
                  {a.decidedAt && <span className="text-zinc-500 ml-2">{new Date(a.decidedAt).toLocaleString()}</span>}
                </div>
              ))}
            </CardContent>
          </Card>
        )}
      </div>

      {showScheduleModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4" onClick={() => setShowScheduleModal(false)}>
          <Card className="w-full max-w-md p-6 space-y-4" onClick={(e) => e.stopPropagation()}>
            <h2 className="text-lg font-semibold text-zinc-100">Schedule Production Release</h2>
            <p className="text-sm text-zinc-400">Set the deployment window for ticket #{ticket.ticketNumber} after UAT acceptance.</p>
            <div>
              <label className="text-sm text-zinc-400 mb-1 block">Scheduled date & time</label>
              <Input type="datetime-local" value={scheduleDate} onChange={(e) => setScheduleDate(e.target.value)} />
            </div>
            <div>
              <label className="text-sm text-zinc-400 mb-1 block">Release window</label>
              <select
                value={releaseWindow}
                onChange={(e) => setReleaseWindow(e.target.value)}
                className="flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100"
              >
                {["Maintenance", "Business Hours", "Off Hours", "Emergency"].map((w) => (
                  <option key={w} value={w}>{w}</option>
                ))}
              </select>
            </div>
            <div className="flex gap-2 justify-end">
              <Button variant="secondary" onClick={() => setShowScheduleModal(false)}>Cancel</Button>
              <Button onClick={scheduleRelease} disabled={scheduling}>
                {scheduling ? "Scheduling..." : "Schedule Release"}
              </Button>
            </div>
          </Card>
        </div>
      )}
    </AppLayout>
  );
}
