import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';

const API_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5000/api/v1';

interface DashboardStats {
  openTickets: number;
  pendingApprovals: number;
  averageRiskScore: number;
}

export default function HomeScreen() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Mobile scaffold: wire to API when auth token is available via secure storage
    const token = process.env.EXPO_PUBLIC_DEMO_TOKEN;
    if (!token) {
      setStats({ openTickets: 0, pendingApprovals: 0, averageRiskScore: 0 });
      setLoading(false);
      return;
    }

    fetch(`${API_URL}/workspaces/demo/dashboard`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then((r) => r.json())
      .then((body) => {
        if (body.data) {
          setStats({
            openTickets: body.data.openTickets,
            pendingApprovals: body.data.pendingApprovals,
            averageRiskScore: body.data.averageRiskScore,
          });
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

        {loading ? (
          <ActivityIndicator color="#6366f1" style={{ marginTop: 32 }} />
        ) : error ? (
          <Text style={styles.error}>{error}</Text>
        ) : (
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
        )}

        <TouchableOpacity style={styles.button}>
          <Text style={styles.buttonText}>Review Approvals</Text>
        </TouchableOpacity>

        <TouchableOpacity style={[styles.button, styles.secondaryButton]}>
          <Text style={styles.buttonText}>View Deployments</Text>
        </TouchableOpacity>

        <Text style={styles.hint}>Set EXPO_PUBLIC_DEMO_TOKEN for live API data</Text>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#09090b' },
  content: { padding: 24, paddingTop: 60 },
  title: { fontSize: 28, fontWeight: '700', color: '#fafafa' },
  subtitle: { fontSize: 14, color: '#71717a', marginBottom: 32 },
  error: { color: '#f87171', marginTop: 16 },
  hint: { color: '#52525b', fontSize: 12, marginTop: 24, textAlign: 'center' },
  card: { backgroundColor: '#18181b', borderRadius: 12, padding: 20, marginBottom: 12, borderWidth: 1, borderColor: '#27272a' },
  cardTitle: { fontSize: 14, color: '#a1a1aa' },
  cardValue: { fontSize: 32, fontWeight: '700', color: '#fafafa', marginTop: 4 },
  button: { backgroundColor: '#6366f1', borderRadius: 12, padding: 16, alignItems: 'center', marginTop: 12 },
  secondaryButton: { backgroundColor: '#27272a' },
  buttonText: { color: '#fafafa', fontSize: 16, fontWeight: '600' },
});
