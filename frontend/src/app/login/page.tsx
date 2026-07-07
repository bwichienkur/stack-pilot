"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Bot } from "lucide-react";
import { Button, Input, Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";

export default function LoginPage() {
  const [email, setEmail] = useState("admin@stackpilot.io");
  const [password, setPassword] = useState("StackPilot123!");
  const [isRegister, setIsRegister] = useState(false);
  const [firstName, setFirstName] = useState("Admin");
  const [lastName, setLastName] = useState("User");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const { setAuth } = useAuth();
  const router = useRouter();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const endpoint = isRegister ? "/auth/register" : "/auth/login";
      const body = isRegister
        ? { email, password, firstName, lastName }
        : { email, password };

      const data = await api<{ accessToken: string; user: { id: string; email: string; firstName?: string; lastName?: string } }>(
        endpoint, { method: "POST", body: JSON.stringify(body) }
      );

      setAuth(data.accessToken, data.user);
      router.push("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Authentication failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-zinc-950 relative overflow-hidden">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-indigo-900/20 via-zinc-950 to-zinc-950" />
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-indigo-500/5 rounded-full blur-3xl" />
      <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-purple-500/5 rounded-full blur-3xl" />

      <Card className="w-full max-w-md relative z-10 border-zinc-800/50 shadow-2xl">
        <CardHeader className="text-center">
          <div className="mx-auto h-14 w-14 rounded-2xl bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center mb-4 shadow-lg shadow-indigo-500/25">
            <Bot className="h-8 w-8 text-white" />
          </div>
          <CardTitle className="text-2xl">Welcome to StackPilot</CardTitle>
          <CardDescription>Enterprise Software Intelligence Platform</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            {isRegister && (
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">First Name</label>
                  <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} required />
                </div>
                <div>
                  <label className="text-sm text-zinc-400 mb-1 block">Last Name</label>
                  <Input value={lastName} onChange={(e) => setLastName(e.target.value)} required />
                </div>
              </div>
            )}
            <div>
              <label className="text-sm text-zinc-400 mb-1 block">Email</label>
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div>
              <label className="text-sm text-zinc-400 mb-1 block">Password</label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
            {error && <p className="text-sm text-red-400">{error}</p>}
            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? "Please wait..." : isRegister ? "Create Account" : "Sign In"}
            </Button>
          </form>
          <div className="mt-4 text-center">
            <button
              onClick={() => setIsRegister(!isRegister)}
              className="text-sm text-indigo-400 hover:text-indigo-300 transition-colors"
            >
              {isRegister ? "Already have an account? Sign in" : "Need an account? Register"}
            </button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
