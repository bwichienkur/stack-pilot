"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";

export interface DashboardStats {
  applicationCount: number;
  repositoryCount: number;
  databaseCount: number;
  openTickets: number;
  pendingApprovals: number;
  openRecommendations: number;
  averageRiskScore: number;
  activeConnectors: number;
  highRiskCount?: number;
  unhealthyConnectors?: number;
  pendingQaCount?: number;
  pendingUatCount?: number;
  releasesThisWeek?: number;
}

export interface ApprovalGate {
  id: string;
  gateType: string;
  requiredPermission: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface OrganizationInvite {
  id: string;
  email: string;
  roleName: string;
  invitedAt: string;
  expiresAt: string;
}

export interface OrganizationInviteCreated {
  id: string;
  email: string;
  roleName: string;
  expiresAt: string;
  token: string;
  inviteUrl: string;
}

export interface InvitableRole {
  id: string;
  name: string;
  description?: string;
}

export interface OutboundWebhook {
  id: string;
  url: string;
  events: string[];
  isActive: boolean;
  createdAt: string;
}

export interface SamlConfig {
  enabled: boolean;
  entityId?: string;
  idpMetadataUrl?: string;
  idpCertificate?: string;
  loginUrl?: string;
  metadataUrl?: string;
}

export interface DocPageLatest {
  pageId?: string;
  title?: string;
  version: number;
  contentMd: string;
  generatedBy: string;
  status: string;
  createdAt: string;
  versions?: { version: number; generatedBy: string; status: string; createdAt: string }[];
}

export interface AdminOrganization {
  id: string;
  name: string;
  slug: string;
  plan: string;
  isActive: boolean;
  memberCount: number;
  createdAt: string;
}

export interface TicketWorkflowStates {
  currentStatus: string;
  allowedTransitions: string[];
}

export function useDashboard() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["dashboard", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () =>
      api<DashboardStats>(`/workspaces/${workspaceId}/dashboard`, {}, token, orgId, workspaceId),
  });
}

export function useConnectors() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["connectors", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () =>
      api<{ id: string; name: string; type: string; status: string; healthStatus: string; lastSyncAt?: string }[]>(
        `/workspaces/${workspaceId}/connectors`,
        {},
        token,
        orgId,
        workspaceId
      ),
  });
}

export function useApplications() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["applications", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () =>
      api<{ id: string; nodeType: string; name: string; riskScore?: number; metadataJson?: string }[]>(
        `/workspaces/${workspaceId}/applications`,
        {},
        token,
        orgId,
        workspaceId
      ),
  });
}

export function useRecommendations() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["recommendations", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () =>
      api<{ items: { id: string; type: string; summary: string; riskLevel: string; confidenceScore?: number; status: string; createdAt: string }[] }>(
        `/workspaces/${workspaceId}/recommendations?page=1&pageSize=50`,
        {},
        token,
        orgId,
        workspaceId
      ),
  });
}

export function useDeployments() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["deployments", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () =>
      api<{ id: string; status: string; conclusion?: string; logsUrl?: string; pullRequestUrl?: string; startedAt?: string; completedAt?: string }[]>(
        `/workspaces/${workspaceId}/build-runs`,
        {},
        token,
        orgId,
        workspaceId
      ),
  });
}

export function useAuditLogs(page: number, pageSize = 25) {
  const { token, orgId } = useAuth();
  return useQuery({
    queryKey: ["audit-logs", orgId, page, pageSize],
    enabled: !!token && !!orgId,
    queryFn: () =>
      api<{ items: { id: string; action: string; entityType?: string; entityId?: string; userId?: string; detailsJson?: string; createdAt: string }[]; totalCount: number; page: number; pageSize: number }>(
        `/organizations/${orgId}/audit-logs?page=${page}&pageSize=${pageSize}`,
        {},
        token,
        orgId
      ),
  });
}

export function useApprovalGates() {
  const { token, orgId } = useAuth();
  return useQuery({
    queryKey: ["approval-gates", orgId],
    enabled: !!token && !!orgId,
    queryFn: () => api<ApprovalGate[]>(`/organizations/${orgId}/approval-gates`, {}, token, orgId),
  });
}

export function useUpdateApprovalGates() {
  const { token, orgId } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (gates: { id: string; isEnabled: boolean; sortOrder?: number }[]) =>
      api<ApprovalGate[]>(
        `/organizations/${orgId}/approval-gates`,
        { method: "PUT", body: JSON.stringify({ gates }) },
        token,
        orgId
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["approval-gates", orgId] }),
  });
}

export function useDocPage(pageId: string) {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["doc-page", pageId],
    enabled: !!token && !!pageId,
    queryFn: () => api<DocPageLatest>(`/docs/${pageId}/latest`, {}, token, orgId, workspaceId),
  });
}

export function useTickets() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["tickets", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<{ items: unknown[] }>(`/workspaces/${workspaceId}/tickets?page=1&pageSize=100`, {}, token, orgId, workspaceId),
  });
}

export function usePendingApprovals() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["approvals", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<unknown[]>(`/workspaces/${workspaceId}/approvals/pending`, {}, token, orgId, workspaceId),
  });
}

export function usePendingQa() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["qa", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<unknown[]>(`/workspaces/${workspaceId}/qa/pending`, {}, token, orgId, workspaceId),
  });
}

export function usePendingUat() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["uat", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<unknown[]>(`/workspaces/${workspaceId}/uat/pending`, {}, token, orgId, workspaceId),
  });
}

export function useDocs() {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["docs", workspaceId],
    enabled: !!token && !!workspaceId,
    queryFn: () => api<{ items: { id: string; title: string; docType: string; latestVersion: number; status: string }[] }>(
      `/workspaces/${workspaceId}/docs?page=1&pageSize=50`, {}, token, orgId, workspaceId
    ),
  });
}

export function useGenerateDoc() {
  const { token, orgId, workspaceId } = useAuth();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (pageId: string) =>
      api<string>(`/docs/${pageId}/generate`, { method: "POST" }, token, orgId, workspaceId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["docs", workspaceId] }),
  });
}

export function useAdminOrganizations(enabled: boolean) {
  const { token } = useAuth();
  return useQuery({
    queryKey: ["admin-organizations"],
    enabled: !!token && enabled,
    queryFn: () => api<AdminOrganization[]>("/admin/organizations", {}, token),
    retry: false,
  });
}

export function useIsSuperAdmin() {
  const { token, orgId, user } = useAuth();
  return useQuery({
    queryKey: ["super-admin-check", orgId, user?.id],
    enabled: !!token && !!orgId,
    queryFn: async () => {
      try {
        await api<AdminOrganization[]>("/admin/organizations?limit=1", {}, token);
        return true;
      } catch {
        const members = await api<{ userId: string; roleName: string }[]>(
          `/organizations/${orgId}/members`,
          {},
          token,
          orgId
        ).catch(() => []);
        const me = members.find((m) => m.userId === user?.id);
        return me?.roleName === "Platform Super Admin" || me?.roleName === "PlatformSuperAdmin";
      }
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

export function useTicketWorkflowStates(ticketId: string) {
  const { token, orgId, workspaceId } = useAuth();
  return useQuery({
    queryKey: ["ticket-workflow", ticketId],
    enabled: !!token && !!ticketId,
    queryFn: () =>
      api<TicketWorkflowStates>(`/tickets/${ticketId}/workflow-states`, {}, token, orgId, workspaceId),
    retry: false,
  });
}
