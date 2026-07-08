"use client";

import { Suspense } from "react";
import AcceptInviteForm from "./accept-form";

export default function AcceptInvitePage() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center bg-zinc-950 text-zinc-400">Loading...</div>}>
      <AcceptInviteForm />
    </Suspense>
  );
}
