"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";

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
