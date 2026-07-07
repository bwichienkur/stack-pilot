"use client";

import { useState } from "react";
import { Bot, X, Send } from "lucide-react";
import { Button, Input } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { cn } from "@/lib/utils";

export function AiCopilotPanel() {
  const { token, orgId, workspaceId } = useAuth();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [reply, setReply] = useState("");
  const [loading, setLoading] = useState(false);
  const [conversationId, setConversationId] = useState<string | null>(null);

  const send = async () => {
    if (!message.trim() || !token || !workspaceId) return;
    setLoading(true);
    try {
      const data = await api<{ reply: string; conversationId: string }>(
        `/workspaces/${workspaceId}/ai/chat`,
        {
          method: "POST",
          body: JSON.stringify({ message, conversationId }),
        },
        token, orgId, workspaceId
      );
      setReply(data.reply);
      setConversationId(data.conversationId);
      setMessage("");
    } catch {
      setReply("Sorry, I could not process that request.");
    } finally {
      setLoading(false);
    }
  };

  if (!token) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="fixed bottom-6 right-6 z-40 h-12 w-12 rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-white shadow-lg flex items-center justify-center hover:scale-105 transition-transform"
        aria-label="Open AI copilot"
      >
        <Bot className="h-6 w-6" />
      </button>

      <div className={cn(
        "fixed bottom-0 right-0 z-50 w-full sm:w-96 h-[70vh] bg-zinc-950 border-l border-t border-zinc-800 shadow-2xl flex flex-col transition-transform duration-300",
        open ? "translate-x-0" : "translate-x-full"
      )}>
        <div className="flex items-center justify-between px-4 h-14 border-b border-zinc-800">
          <div className="flex items-center gap-2">
            <Bot className="h-5 w-5 text-indigo-400" />
            <span className="font-medium text-zinc-100">StackPilot AI</span>
          </div>
          <button type="button" onClick={() => setOpen(false)} className="text-zinc-400 hover:text-zinc-200">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-3">
          {!reply && !loading && (
            <p className="text-sm text-zinc-500">Ask about your architecture, tickets, or risk areas. Answers are grounded in your workspace graph.</p>
          )}
          {reply && (
            <div className="rounded-lg bg-zinc-900 border border-zinc-800 p-3 text-sm text-zinc-300 whitespace-pre-wrap">{reply}</div>
          )}
          {loading && <p className="text-sm text-zinc-500 animate-pulse">Thinking...</p>}
        </div>

        <div className="p-4 border-t border-zinc-800 flex gap-2">
          <Input
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && send()}
            placeholder="Ask StackPilot..."
            disabled={!workspaceId || loading}
          />
          <Button size="sm" onClick={send} disabled={!workspaceId || loading || !message.trim()}>
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </>
  );
}
