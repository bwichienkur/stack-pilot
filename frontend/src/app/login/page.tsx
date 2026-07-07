"use client";

import { Suspense } from "react";
import LoginForm from "./login-form";

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center bg-zinc-950 text-zinc-400">Loading...</div>}>
      <LoginForm />
    </Suspense>
  );
}
