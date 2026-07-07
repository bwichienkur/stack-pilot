"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bot, Check } from "lucide-react";
import { Button, Card, Badge } from "@/components/ui";
import { api } from "@/lib/utils";
import { useAuth } from "@/lib/auth-context";

interface PlanLimits {
  includedSeats: number;
  maxSeats: number;
  maxWorkspaces: number;
  maxConnectors: number;
  monthlyAiTokenBudget: number;
  auditRetentionDays: number;
  samlSso: boolean;
  prioritySupport: boolean;
}

interface Plan {
  plan: string;
  name: string;
  tagline: string;
  monthlyPriceUsd: number | null;
  annualPriceUsd: number | null;
  additionalSeatPriceUsd: number;
  billingModel: string;
  limits: PlanLimits;
  highlights: string[];
}

function formatPrice(plan: Plan, interval: "monthly" | "annual") {
  if (plan.billingModel === "custom") return "Custom";
  if (plan.billingModel === "free") return "Free";
  const price = interval === "annual" ? plan.annualPriceUsd : plan.monthlyPriceUsd;
  if (price == null) return "Custom";
  if (interval === "annual") return `$${price.toLocaleString()}/yr`;
  return `$${price.toLocaleString()}/mo`;
}

function formatTokens(n: number) {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(n % 1_000_000 === 0 ? 0 : 1)}M`;
  if (n >= 1_000) return `${(n / 1_000).toFixed(0)}K`;
  return String(n);
}

export default function PricingPage() {
  const { token } = useAuth();
  const router = useRouter();
  const [plans, setPlans] = useState<Plan[]>([]);
  const [interval, setInterval] = useState<"monthly" | "annual">("monthly");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api<Plan[]>("/billing/plans")
      .then(setPlans)
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  const startUpgrade = (planName: string) => {
    if (!token) {
      router.push("/login");
      return;
    }
    if (planName === "Enterprise") {
      window.location.href = "mailto:sales@stackpilot.dev?subject=StackPilot%20Enterprise";
      return;
    }
    router.push(`/settings?upgrade=${planName.toLowerCase()}`);
  };

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100">
      <header className="border-b border-zinc-800">
        <div className="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">
          <Link href={token ? "/" : "/login"} className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-indigo-500 to-purple-600 flex items-center justify-center">
              <Bot className="h-5 w-5 text-white" />
            </div>
            <span className="font-semibold">StackPilot</span>
          </Link>
          <div className="flex gap-3">
            {token ? (
              <Link href="/"><Button variant="secondary" size="sm">Dashboard</Button></Link>
            ) : (
              <Link href="/login"><Button variant="secondary" size="sm">Sign in</Button></Link>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-6xl mx-auto px-6 py-16 space-y-12">
        <div className="text-center space-y-4">
          <h1 className="text-4xl font-bold tracking-tight">Simple pricing for governed change</h1>
          <p className="text-zinc-400 max-w-2xl mx-auto">
            Start with a 14-day trial. Design partners convert with 20% off year one when signing within 30 days of pilot end.
          </p>
          <div className="inline-flex rounded-lg border border-zinc-700 p-1 gap-1">
            <button
              type="button"
              onClick={() => setInterval("monthly")}
              className={`px-4 py-1.5 text-sm rounded-md transition-colors ${interval === "monthly" ? "bg-indigo-600 text-white" : "text-zinc-400 hover:text-zinc-200"}`}
            >
              Monthly
            </button>
            <button
              type="button"
              onClick={() => setInterval("annual")}
              className={`px-4 py-1.5 text-sm rounded-md transition-colors ${interval === "annual" ? "bg-indigo-600 text-white" : "text-zinc-400 hover:text-zinc-200"}`}
            >
              Annual <Badge variant="success" className="ml-1">Save ~17%</Badge>
            </button>
          </div>
        </div>

        {loading ? (
          <p className="text-center text-zinc-500">Loading plans…</p>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {plans.map((plan) => {
              const featured = plan.plan === "Professional";
              return (
                <Card
                  key={plan.plan}
                  className={`p-6 flex flex-col ${featured ? "border-indigo-500/50 ring-1 ring-indigo-500/20" : ""}`}
                >
                  {featured && <Badge className="mb-3 w-fit">Most popular</Badge>}
                  <h2 className="text-xl font-semibold">{plan.name}</h2>
                  <p className="text-sm text-zinc-400 mt-1 min-h-[40px]">{plan.tagline}</p>
                  <p className="text-3xl font-bold mt-4">{formatPrice(plan, interval)}</p>
                  {plan.additionalSeatPriceUsd > 0 && plan.billingModel !== "custom" && (
                    <p className="text-xs text-zinc-500 mt-1">+${plan.additionalSeatPriceUsd}/extra seat</p>
                  )}
                  <ul className="mt-6 space-y-2 flex-1">
                    {plan.highlights.map((h) => (
                      <li key={h} className="flex gap-2 text-sm text-zinc-300">
                        <Check className="h-4 w-4 text-emerald-400 shrink-0 mt-0.5" />
                        {h}
                      </li>
                    ))}
                  </ul>
                  <div className="mt-6 text-xs text-zinc-500 space-y-1 border-t border-zinc-800 pt-4">
                    <p>{formatTokens(plan.limits.monthlyAiTokenBudget)} AI tokens / mo</p>
                    <p>{plan.limits.auditRetentionDays}-day audit retention</p>
                    {plan.limits.samlSso && <p>SAML SSO included</p>}
                  </div>
                  <Button
                    className="w-full mt-6"
                    variant={featured ? "default" : "secondary"}
                    onClick={() => startUpgrade(plan.plan)}
                  >
                    {plan.plan === "Trial" ? "Start trial" : plan.plan === "Enterprise" ? "Contact sales" : "Upgrade"}
                  </Button>
                </Card>
              );
            })}
          </div>
        )}

        <Card className="p-8 bg-indigo-950/20 border-indigo-500/20">
          <h2 className="text-lg font-semibold mb-2">Design partner offer</h2>
          <p className="text-sm text-zinc-400 max-w-3xl">
            Pilots are free for 8–12 weeks. Convert within 30 days of pilot completion for{" "}
            <strong className="text-zinc-200">20% off your first year</strong> (coupon: DESIGNPARTNER20 in Stripe).
          </p>
        </Card>
      </main>
    </div>
  );
}
