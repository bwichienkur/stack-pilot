"use client";

import { Component, type ErrorInfo, type ReactNode } from "react";
import { Button, Card } from "@/components/ui";
import { AlertTriangle } from "lucide-react";

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
  message: string;
}

export class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, message: "" };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, message: error.message || "Something went wrong" };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error("ErrorBoundary caught:", error, info.componentStack);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="min-h-screen bg-zinc-950 flex items-center justify-center p-6">
          <Card className="max-w-md w-full p-8 text-center space-y-4">
            <div className="mx-auto h-12 w-12 rounded-xl bg-red-500/10 flex items-center justify-center">
              <AlertTriangle className="h-6 w-6 text-red-400" />
            </div>
            <h1 className="text-xl font-bold text-zinc-100">Something went wrong</h1>
            <p className="text-sm text-zinc-400">{this.state.message}</p>
            <Button onClick={() => this.setState({ hasError: false, message: "" })}>
              Try again
            </Button>
          </Card>
        </div>
      );
    }
    return this.props.children;
  }
}
