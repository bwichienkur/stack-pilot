import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';

const API_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5000/api/v1';

interface DashboardStats {
  openTickets: number;
  pendingApprovals: number;
  averageRiskScore: number;
}

interface ApprovalTicket {
  id: string;
  ticketNumber: number;
  title: string;
  status: string;
  priority: string;
  riskScore?: number;
}

export default function HomeScreen() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [approvals, setApprovals] = useState<ApprovalTicket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [view, setView] = useState<'dashboard' | 'approvals'>('dashboard');

  useEffect(() => {
    const token = process.env.EXPO_PUBLIC_DEMO_TOKEN;
    const workspaceId = process.env.EXPO_PUBLIC_DEMO_WORKSPACE_ID || 'demo';

    if (!token) {
      setStats({ openTickets: 0, pendingApprovals: 0, averageRiskScore: 0 });
      setLoading(false);
      return;
    }

    const headers = { Authorization: `Bearer ${token}` };

    Promise.all([
      fetch(`${API_URL}/workspaces/${workspaceId}/dashboard`, { headers }).then((r) => r.json()),
      fetch(`${API_URL}/workspaces/${workspaceId}/approvals/pending`, { headers }).then((r) => r.json()),
    ])
      .then(([dashboardBody, approvalsBody]) => {
        if (dashboardBody.data) {
          setStats({
            openTickets: dashboardBody.data.openTickets,
            pendingApprovals: dashboardBody.data.pendingApprovals,
            averageRiskScore: dashboardBody.data.averageRiskScore,
          });
        }
        if (Array.isArray(approvalsBody.data)) {
          setApprovals(approvalsBody.data);
        }
      })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  return (
    <View style={styles.container}>
      <StatusBar style="light" />
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>StackPilot</Text>
        <Text style={styles.subtitle}>Enterprise Intelligence — Mobile</Text>

        <View style={styles.tabRow}>
          <TouchableOpacity
            style={[styles.tab, view === 'dashboard' && styles.tabActive]}
            onPress={() => setView('dashboard')}
          >
            <Text style={styles.tabText}>Dashboard</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.tab, view === 'approvals' && styles.tabActive]}
            onPress={() => setView('approvals')}
          >
            <Text style={styles.tabText}>Approvals ({approvals.length})</Text>
          </TouchableOpacity>
        </View>

        {loading ? (
          <ActivityIndicator color="#6366f1" style={{ marginTop: 32 }} />
        ) : error ? (
          <Text style={styles.error}>{error}</Text>
        ) : view === 'dashboard' ? (
          <>
            <View style={styles.card}>
              <Text style={styles.cardTitle}>Pending Approvals</Text>
              <Text style={styles.cardValue}>{stats?.pendingApprovals ?? '—'}</Text>
            </View>

            <View style={styles.card}>
              <Text style={styles.cardTitle}>Open Tickets</Text>
              <Text style={styles.cardValue}>{stats?.openTickets ?? '—'}</Text>
            </View>

            <View style={styles.card}>
              <Text style={styles.cardTitle}>Avg Risk Score</Text>
              <Text style={styles.cardValue}>{stats?.averageRiskScore?.toFixed(1) ?? '—'}</Text>
            </View>
          </>
        ) : (
          <>
            {approvals.length === 0 ? (
              <Text style={styles.emptyText}>No pending approvals</Text>
            ) : (
              approvals.map((t) => (
                <View key={t.id} style={styles.card}>
                  <Text style={styles.approvalMeta}>#{t.ticketNumber} · {t.status}</Text>
                  <Text style={styles.approvalTitle}>{t.title}</Text>
                  <Text style={styles.approvalMeta}>{t.priority}{t.riskScore != null ? ` · Risk ${t.riskScore}` : ''}</Text>
                  <Text style={styles.readOnlyHint}>Read-only — approve on web</Text>
                </View>
              ))
            )}
          </>
        )}

        <Text style={styles.hint}>Set EXPO_PUBLIC_DEMO_TOKEN for live API data</Text>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#09090b' },
  content: { padding: 24, paddingTop: 60 },
  title: { fontSize: 28, fontWeight: '700', color: '#fafafa' },
  subtitle: { fontSize: 14, color: '#71717a', marginBottom: 24 },
  tabRow: { flexDirection: 'row', gap: 8, marginBottom: 16 },
  tab: { flex: 1, paddingVertical: 10, borderRadius: 8, backgroundColor: '#18181b', alignItems: 'center', borderWidth: 1, borderColor: '#27272a' },
  tabActive: { backgroundColor: '#312e81', borderColor: '#6366f1' },
  tabText: { color: '#fafafa', fontSize: 13, fontWeight: '600' },
  error: { color: '#f87171', marginTop: 16 },
  emptyText: { color: '#71717a', marginTop: 16, textAlign: 'center' },
  hint: { color: '#52525b', fontSize: 12, marginTop: 24, textAlign: 'center' },
  card: { backgroundColor: '#18181b', borderRadius: 12, padding: 20, marginBottom: 12, borderWidth: 1, borderColor: '#27272a' },
  cardTitle: { fontSize: 14, color: '#a1a1aa' },
  cardValue: { fontSize: 32, fontWeight: '700', color: '#fafafa', marginTop: 4 },
  approvalMeta: { fontSize: 12, color: '#71717a', marginBottom: 4 },
  approvalTitle: { fontSize: 16, fontWeight: '600', color: '#fafafa', marginBottom: 8 },
  readOnlyHint: { fontSize: 11, color: '#6366f1', marginTop: 8 },
});
