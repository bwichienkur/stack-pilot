"use client";

import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent } from "@/components/ui";

export default function StubPage({ title, description }: { title: string; description: string }) {
  return (
    <AppLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-zinc-100">{title}</h1>
          <p className="text-zinc-400 mt-1">{description}</p>
        </div>
        <Card>
          <CardContent className="p-12 text-center">
            <p className="text-zinc-400">This module is scaffolded and ready for implementation.</p>
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}
