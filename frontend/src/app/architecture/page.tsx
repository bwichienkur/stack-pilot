"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ReactFlow, Background, Controls, MiniMap, useNodesState, useEdgesState,
  addEdge, Connection, Node, Edge, MarkerType
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { AppLayout } from "@/components/layout/sidebar";
import { Card, Badge, Button } from "@/components/ui";
import { useAuth } from "@/lib/auth-context";
import { api } from "@/lib/utils";
import { Filter, Maximize2 } from "lucide-react";

const nodeColors: Record<string, string> = {
  Application: "#6366f1",
  Repository: "#8b5cf6",
  Database: "#06b6d4",
  Table: "#14b8a6",
  ApiEndpoint: "#f59e0b",
  Pipeline: "#ec4899",
};

function CustomNode({ data }: { data: { label: string; type: string; risk?: number } }) {
  const color = nodeColors[data.type] || "#71717a";
  return (
    <div className="px-4 py-3 rounded-xl border-2 shadow-lg min-w-[160px]" style={{ borderColor: color, background: `${color}15` }}>
      <div className="text-[10px] uppercase tracking-wider font-medium mb-1" style={{ color }}>{data.type}</div>
      <div className="text-sm font-semibold text-zinc-100">{data.label}</div>
      {data.risk !== undefined && data.risk > 0 && (
        <div className="mt-2"><Badge variant={data.risk > 5 ? "danger" : "warning"}>Risk: {data.risk}</Badge></div>
      )}
    </div>
  );
}

const nodeTypes = { custom: CustomNode };

export default function ArchitecturePage() {
  const { token, orgId, workspaceId } = useAuth();
  const router = useRouter();
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [selectedNode, setSelectedNode] = useState<Node | null>(null);

  useEffect(() => {
    if (!token) { router.push("/login"); return; }
    if (workspaceId) loadGraph();
  }, [token, workspaceId]);

  const loadGraph = async () => {
    try {
      const [nodesData, edgesData] = await Promise.all([
        api<{ items: { id: string; nodeType: string; name: string; riskScore?: number }[] }>(
          `/workspaces/${workspaceId}/graph/nodes?page=1&pageSize=50`, {}, token, orgId, workspaceId
        ),
        api<{ id: string; sourceNodeId: string; targetNodeId: string; edgeType: string }[]>(
          `/workspaces/${workspaceId}/graph/edges`, {}, token, orgId, workspaceId
        ),
      ]);

      const flowNodes: Node[] = (nodesData.items || []).map((n, i) => ({
        id: n.id,
        type: "custom",
        position: { x: (i % 4) * 250 + 50, y: Math.floor(i / 4) * 150 + 50 },
        data: { label: n.name, type: n.nodeType, risk: n.riskScore },
      }));

      if (flowNodes.length === 0) {
        flowNodes.push(
          { id: "1", type: "custom", position: { x: 250, y: 100 }, data: { label: "Order API", type: "Application", risk: 3 } },
          { id: "2", type: "custom", position: { x: 50, y: 250 }, data: { label: "order-service", type: "Repository" } },
          { id: "3", type: "custom", position: { x: 450, y: 250 }, data: { label: "orders_db", type: "Database" } },
          { id: "4", type: "custom", position: { x: 250, y: 400 }, data: { label: "orders", type: "Table" } },
        );
      }

      const flowEdges: Edge[] = (edgesData || []).map((e) => ({
        id: e.id,
        source: e.sourceNodeId,
        target: e.targetNodeId,
        label: e.edgeType,
        animated: true,
        style: { stroke: "#6366f1" },
        markerEnd: { type: MarkerType.ArrowClosed, color: "#6366f1" },
      }));

      if (flowEdges.length === 0 && flowNodes.length > 1) {
        flowEdges.push(
          { id: "e1-2", source: "1", target: "2", label: "owns", animated: true, style: { stroke: "#6366f1" }, markerEnd: { type: MarkerType.ArrowClosed, color: "#6366f1" } },
          { id: "e1-3", source: "1", target: "3", label: "reads_from", animated: true, style: { stroke: "#06b6d4" }, markerEnd: { type: MarkerType.ArrowClosed, color: "#06b6d4" } },
          { id: "e3-4", source: "3", target: "4", label: "contains", animated: true, style: { stroke: "#14b8a6" }, markerEnd: { type: MarkerType.ArrowClosed, color: "#14b8a6" } },
        );
      }

      setNodes(flowNodes);
      setEdges(flowEdges);
    } catch (err) {
      console.error("Graph load error:", err);
    }
  };

  const onConnect = useCallback((params: Connection) => setEdges((eds) => addEdge(params, eds)), [setEdges]);

  if (!token) return null;

  return (
    <AppLayout>
      <div className="space-y-4 h-[calc(100vh-8rem)]">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold text-zinc-100">Architecture Map</h1>
            <p className="text-zinc-400 mt-1">Interactive dependency and relationship visualization</p>
          </div>
          <div className="flex gap-2">
            <Button variant="secondary" size="sm"><Filter className="h-4 w-4" /> Filters</Button>
            <Button variant="secondary" size="sm"><Maximize2 className="h-4 w-4" /> Fullscreen</Button>
          </div>
        </div>

        <div className="flex gap-4 h-full">
          <Card className="flex-1 overflow-hidden">
            <ReactFlow
              nodes={nodes}
              edges={edges}
              onNodesChange={onNodesChange}
              onEdgesChange={onEdgesChange}
              onConnect={onConnect}
              onNodeClick={(_, node) => setSelectedNode(node)}
              nodeTypes={nodeTypes}
              fitView
              className="bg-zinc-950"
            >
              <Background color="#27272a" gap={20} />
              <Controls className="!bg-zinc-900 !border-zinc-700 !rounded-lg [&>button]:!bg-zinc-800 [&>button]:!border-zinc-700 [&>button]:!text-zinc-300" />
              <MiniMap className="!bg-zinc-900 !border-zinc-700 !rounded-lg" nodeColor={(n) => nodeColors[n.data?.type as string] || "#71717a"} />
            </ReactFlow>
          </Card>

          {selectedNode && (
            <Card className="w-80 flex-shrink-0">
              <div className="p-6 space-y-4">
                <h3 className="text-lg font-semibold text-zinc-100">{selectedNode.data.label as string}</h3>
                <Badge>{selectedNode.data.type as string}</Badge>
                {selectedNode.data.risk !== undefined && (
                  <div>
                    <p className="text-sm text-zinc-400 mb-1">Risk Score</p>
                    <p className="text-2xl font-bold text-zinc-100">{selectedNode.data.risk as number}</p>
                  </div>
                )}
                <div className="pt-4 border-t border-zinc-800">
                  <p className="text-sm text-zinc-400">Click nodes to explore relationships and impact analysis.</p>
                </div>
              </div>
            </Card>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
