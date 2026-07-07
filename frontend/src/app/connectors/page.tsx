"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, Badge, Button, Input } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { Plus, RefreshCw, CheckCircle } from "lucide-react";

interface ConnectorDef { id: string; type: string; name: string; description?: string; capabilities: string[] }
interface Connector { id: string; name: string; type: string; status: string; healthStatus: string; lastSyncAt?: string }

export default function ConnectorsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const [definitions, setDefinitions] = useState<ConnectorDef[]>([]);
  const [connectors, setConnectors] = useState<Connector[]>([]);
  const [showAdd, setShowAdd] = useState(false);
  const [selectedDef, setSelectedDef] = useState("");
  const [connectorName, setConnectorName] = useState("");
  const [owner, setOwner] = useState("");
  const [pat, setPat] = useState("");

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    loadData();
  }, [token, workspaceId]);

  const loadData = async () => {
    try {
      const defs = await api<ConnectorDef[]>("/connectors/definitions", {}, token);
      setDefinitions(defs);
      if (workspaceId) {
        const conns = await api<Connector[]>(`/workspaces/${workspaceId}/connectors`, {}, token, orgId, workspaceId);
        setConnectors(conns);
      }
    } catch (err) {
      console.error(err);
    }
  };

  const addConnector = async () => {
    if (!workspaceId || !selectedDef) return;
    const def = definitions.find((d) => d.id === selectedDef);
    if (!def) return;

    await api(`/workspaces/${workspaceId}/connectors`, {
      method: "POST",
      body: JSON.stringify({
        name: connectorName,
        definitionId: selectedDef,
        configJson: JSON.stringify({ owner, repositories: "all" }),
        credentials: { pat: pat || undefined }
      })
    }, token, orgId, workspaceId);

    setShowAdd(false);
    loadData();
  };

  const testConnector = async (id: string) => {
    await api(`/connectors/${id}/test`, { method: "POST" }, token, orgId);
    loadData();
  };

  const syncConnector = async (id: string) => {
    await api(`/connectors/${id}/sync`, { method: "POST" }, token, orgId);
    loadData();
  };

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Connectors</h1>
            <p className="text-zinc-400 mt-1">Manage repository, database, and CI/CD connections</p>
          </div>
          <Button onClick={() => setShowAdd(true)}><Plus className="h-4 w-4" /> Add Connector</Button>
        </div>

        {showAdd && (
          <Card>
            <CardContent className="p-6 space-y-4">
              <h3 className="text-lg font-semibold text-zinc-100">Add Connector</h3>
              <select value={selectedDef} onChange={(e) => setSelectedDef(e.target.value)} className="flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100">
                <option value="">Select connector type</option>
                {definitions.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
              <Input placeholder="Connector name" value={connectorName} onChange={(e) => setConnectorName(e.target.value)} />
              <Input placeholder="Owner / Server" value={owner} onChange={(e) => setOwner(e.target.value)} />
              <Input type="password" placeholder="Personal Access Token" value={pat} onChange={(e) => setPat(e.target.value)} />
              <div className="flex gap-2">
                <Button onClick={addConnector}>Create</Button>
                <Button variant="secondary" onClick={() => setShowAdd(false)}>Cancel</Button>
              </div>
            </CardContent>
          </Card>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {connectors.map((c) => (
            <Card key={c.id} className="hover:border-zinc-700 transition-colors">
              <CardContent className="p-6">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="font-semibold text-zinc-100">{c.name}</h3>
                  <Badge variant={c.healthStatus === "Healthy" ? "success" : "warning"}>{c.healthStatus}</Badge>
                </div>
                <p className="text-sm text-zinc-400 mb-1">{c.type}</p>
                <Badge variant="neutral">{c.status}</Badge>
                {c.lastSyncAt && <p className="text-xs text-zinc-500 mt-3">Last sync: {new Date(c.lastSyncAt).toLocaleString()}</p>}
                <div className="flex gap-2 mt-4">
                  <Button variant="secondary" size="sm" onClick={() => testConnector(c.id)}><CheckCircle className="h-3 w-3" /> Test</Button>
                  <Button variant="secondary" size="sm" onClick={() => syncConnector(c.id)}><RefreshCw className="h-3 w-3" /> Sync</Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        {connectors.length === 0 && !showAdd && (
          <Card className="p-12 text-center">
            <p className="text-zinc-400 mb-4">No connectors configured yet</p>
            <Button onClick={() => setShowAdd(true)}><Plus className="h-4 w-4" /> Connect your first repository</Button>
          </Card>
        )}

        <div>
          <h2 className="text-lg font-semibold text-zinc-100 mb-4">Available Connectors</h2>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            {definitions.map((d) => (
              <Card key={d.id} className="p-4">
                <h3 className="font-medium text-zinc-200">{d.name}</h3>
                <p className="text-xs text-zinc-500 mt-1">{d.description}</p>
                <div className="flex flex-wrap gap-1 mt-3">
                  {d.capabilities.map((cap) => <Badge key={cap} variant="neutral">{cap}</Badge>)}
                </div>
              </Card>
            ))}
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
