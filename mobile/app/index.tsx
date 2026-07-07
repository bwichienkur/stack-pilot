import { View, Text, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';
import { StatusBar } from 'expo-status-bar';

const API_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:5000/api/v1';

export default function HomeScreen() {
  return (
    <View style={styles.container}>
      <StatusBar style="light" />
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.title}>StackPilot</Text>
        <Text style={styles.subtitle}>Enterprise Intelligence — Mobile</Text>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Pending Approvals</Text>
          <Text style={styles.cardValue}>3</Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Open Tickets</Text>
          <Text style={styles.cardValue}>12</Text>
        </View>

        <View style={styles.card}>
          <Text style={styles.cardTitle}>Risk Score</Text>
          <Text style={styles.cardValue}>4.2</Text>
        </View>

        <TouchableOpacity style={styles.button}>
          <Text style={styles.buttonText}>Review Approvals</Text>
        </TouchableOpacity>

        <TouchableOpacity style={[styles.button, styles.secondaryButton]}>
          <Text style={styles.buttonText}>View Deployments</Text>
        </TouchableOpacity>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#09090b' },
  content: { padding: 24, paddingTop: 60 },
  title: { fontSize: 28, fontWeight: '700', color: '#fafafa' },
  subtitle: { fontSize: 14, color: '#71717a', marginBottom: 32 },
  card: { backgroundColor: '#18181b', borderRadius: 12, padding: 20, marginBottom: 12, borderWidth: 1, borderColor: '#27272a' },
  cardTitle: { fontSize: 14, color: '#a1a1aa' },
  cardValue: { fontSize: 32, fontWeight: '700', color: '#fafafa', marginTop: 4 },
  button: { backgroundColor: '#6366f1', borderRadius: 12, padding: 16, alignItems: 'center', marginTop: 12 },
  secondaryButton: { backgroundColor: '#27272a' },
  buttonText: { color: '#fafafa', fontSize: 16, fontWeight: '600' },
});
