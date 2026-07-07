"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, CardContent, Badge, Button, Input } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { useToast } from "@/components/ui/toast";
import { Plus, RefreshCw, CheckCircle } from "lucide-react";

interface ConnectorDef {
  id: string;
  type: string;
  name: string;
  description?: string;
  category: string;
  capabilities: string[];
}
interface Connector { id: string; name: string; type: string; status: string; healthStatus: string; lastSyncAt?: string }

const CATEGORY_ORDER = ["SourceCode", "Data", "CiCd", "Itsm"] as const;
const CATEGORY_LABELS: Record<string, string> = {
  SourceCode: "Source code",
  Data: "Data",
  CiCd: "CI/CD",
  Itsm: "ITSM & work"
};

export default function ConnectorsPage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();
  const [definitions, setDefinitions] = useState<ConnectorDef[]>([]);
  const [connectors, setConnectors] = useState<Connector[]>([]);
  const [showAdd, setShowAdd] = useState(false);
  const [selectedDef, setSelectedDef] = useState("");
  const [activeCategory, setActiveCategory] = useState<string>("all");
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
      showToast(err instanceof Error ? err.message : "Failed to load connectors", "error");
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

  const categories = CATEGORY_ORDER.filter((c) => definitions.some((d) => d.category === c));
  const filteredDefinitions = activeCategory === "all"
    ? definitions
    : definitions.filter((d) => d.category === activeCategory);

  const groupedDefinitions = CATEGORY_ORDER.reduce<Record<string, ConnectorDef[]>>((acc, cat) => {
    const items = definitions.filter((d) => d.category === cat);
    if (items.length > 0) acc[cat] = items;
    return acc;
  }, {});

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Connectors</h1>
            <p className="text-zinc-400 mt-1">Source code, data, CI/CD, and ITSM integrations</p>
          </div>
          <Button onClick={() => setShowAdd(true)}><Plus className="h-4 w-4" /> Add Connector</Button>
        </div>

        {showAdd && (
          <Card>
            <CardContent className="p-6 space-y-4">
              <h3 className="text-lg font-semibold text-zinc-100">Add Connector</h3>
              <select value={selectedDef} onChange={(e) => setSelectedDef(e.target.value)} className="flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 text-sm text-zinc-100">
                <option value="">Select connector type</option>
                {categories.map((cat) => (
                  <optgroup key={cat} label={CATEGORY_LABELS[cat] ?? cat}>
                    {definitions.filter((d) => d.category === cat).map((d) => (
                      <option key={d.id} value={d.id}>{d.name}</option>
                    ))}
                  </optgroup>
                ))}
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
            <Button onClick={() => setShowAdd(true)}><Plus className="h-4 w-4" /> Connect your first integration</Button>
          </Card>
        )}

        <div>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-zinc-100">Available Connectors</h2>
            <div className="flex flex-wrap gap-2">
              <Button
                variant={activeCategory === "all" ? "default" : "secondary"}
                size="sm"
                onClick={() => setActiveCategory("all")}
              >
                All ({definitions.length})
              </Button>
              {categories.map((cat) => (
                <Button
                  key={cat}
                  variant={activeCategory === cat ? "default" : "secondary"}
                  size="sm"
                  onClick={() => setActiveCategory(cat)}
                >
                  {CATEGORY_LABELS[cat] ?? cat} ({definitions.filter((d) => d.category === cat).length})
                </Button>
              ))}
            </div>
          </div>

          {activeCategory === "all" ? (
            <div className="space-y-8">
              {Object.entries(groupedDefinitions).map(([cat, items]) => (
                <div key={cat}>
                  <h3 className="text-sm font-medium text-zinc-400 uppercase tracking-wide mb-3">
                    {CATEGORY_LABELS[cat] ?? cat}
                  </h3>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                    {items.map((d) => (
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
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              {filteredDefinitions.map((d) => (
                <Card key={d.id} className="p-4">
                  <h3 className="font-medium text-zinc-200">{d.name}</h3>
                  <p className="text-xs text-zinc-500 mt-1">{d.description}</p>
                  <div className="flex flex-wrap gap-1 mt-3">
                    {d.capabilities.map((cap) => <Badge key={cap} variant="neutral">{cap}</Badge>)}
                  </div>
                </Card>
              ))}
            </div>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
