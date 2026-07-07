import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

export default function RootLayout() {
  return (
    <>
      <StatusBar style="light" />
      <Stack screenOptions={{ headerStyle: { backgroundColor: '#09090b' }, headerTintColor: '#fafafa', contentStyle: { backgroundColor: '#09090b' } }} />
    </>
  );
}
