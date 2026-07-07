# StackPilot — Recommended Architecture

## 1. Architecture Style

**Modular Monolith** with Clean Architecture, designed to evolve into microservices by extracting bounded contexts.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                        │
│  Next.js Web App  │  Desktop (Tauri)  │  Mobile (React Native)  │
└────────────────────────────┬────────────────────────────────────┘
                             │ HTTPS / WebSocket
┌────────────────────────────▼────────────────────────────────────┐
│                     StackPilot.Api (ASP.NET Core)                │
│  Controllers │ Middleware │ Auth │ Rate Limiting │ OpenAPI      │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                  StackPilot.Application                          │
│  Commands/Queries │ DTOs │ Validators │ Service Interfaces      │
│  ┌──────────┬──────────┬──────────┬──────────┬──────────┐     │
│  │ Tenancy  │Connector │  Graph   │ Tickets  │    AI    │     │
│  │  Module  │  Module  │  Module  │  Module  │  Module  │     │
│  └──────────┴──────────┴──────────┴──────────┴──────────┘     │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                    StackPilot.Domain                             │
│  Entities │ Value Objects │ Domain Events │ Enums │ Interfaces    │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                 StackPilot.Infrastructure                        │
│  EF Core │ Redis │ Hangfire │ AI Providers │ Connector SDKs     │
│  Secrets │ Blob Storage │ Message Queue │ OpenTelemetry         │
└────────────────────────────┬────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│              StackPilot.Workers (Background Host)                │
│  Scan Jobs │ Sync Jobs │ AI Jobs │ Notification Jobs            │
└─────────────────────────────────────────────────────────────────┘
```

## 2. Technology Stack

| Layer | Technology |
|-------|------------|
| API | ASP.NET Core 8, Minimal APIs + Controllers |
| ORM | Entity Framework Core 8 + PostgreSQL |
| Cache | Redis (StackExchange.Redis) |
| Queue | Hangfire (PostgreSQL storage) → evolve to RabbitMQ/Azure Service Bus |
| Auth | ASP.NET Identity + JWT + OIDC hooks |
| Frontend | Next.js 14 App Router, TypeScript, Tailwind, shadcn/ui |
| Diagrams | React Flow (@xyflow/react) |
| Desktop | Tauri 2 (Rust + WebView) |
| Mobile | React Native (Expo) — approvals & dashboards |
| AI | Abstract `IAiProvider` with OpenAI/Anthropic/Azure OpenAI implementations |
| Observability | OpenTelemetry + Serilog |

## 3. Bounded Contexts (Modules)

1. **Identity & Tenancy** — Orgs, workspaces, users, RBAC, audit
2. **Connectors** — Plugin framework, sync, health
3. **Intelligence** — Repo scanning, DB scanning, indexing
4. **Knowledge Graph** — Nodes, edges, search, impact analysis
5. **Documentation** — Versioned docs, AI generation
6. **Recommendations** — AI-driven suggestions
7. **Workflow** — Tickets, approvals, QA, UAT, releases
8. **AI Engine** — RAG, tool execution, governance
9. **CI/CD** — Build tracking, deployment history

## 4. Multi-Tenancy Model

- **Shared database, shared schema** with `OrganizationId` on all tenant-scoped entities
- Global query filters in EF Core enforce tenant isolation
- Workspace-level scoping for sub-tenant segmentation
- Row-level security policies (PostgreSQL RLS) as defense-in-depth

## 5. Data Flow: Ticket Lifecycle

```
Business Requester → Create Ticket
        ↓
AI Requirements Generator (RAG + Graph context)
        ↓
Awaiting Approval (Architect, Security, DB Admin)
        ↓
AI Implementation Plan → Human Review
        ↓
AI Code Generation → Branch + PR (never direct to prod)
        ↓
GitHub Actions Build/Test → Results linked to ticket
        ↓
QA Evidence Upload → QA Pass/Fail
        ↓
UAT Approval → Accept/Reject
        ↓
Production Release Scheduling → Deploy → Verify → Close
```

## 6. Deployment Architecture

```
                    ┌──────────────┐
                    │   CDN/WAF    │
                    └──────┬───────┘
                           │
              ┌────────────▼────────────┐
              │   Next.js (Vercel/AKS)  │
              └────────────┬────────────┘
                           │
              ┌────────────▼────────────┐
              │  StackPilot API (AKS)   │
              │  StackPilot Workers     │
              └────────────┬────────────┘
         ┌─────────────────┼─────────────────┐
         │                 │                 │
    ┌────▼────┐      ┌─────▼─────┐    ┌─────▼─────┐
    │PostgreSQL│      │   Redis   │    │ Blob Store│
    └─────────┘      └───────────┘    └───────────┘
```

## 7. Service Extraction Path

When scale demands, extract in this order:
1. **Workers** → separate scan/AI processing service
2. **Connectors** → connector gateway service
3. **AI Engine** → dedicated AI orchestration service
4. **Knowledge Graph** → graph database (Neo4j) with sync from PostgreSQL

## 8. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Modular monolith first | Faster MVP, simpler ops, clear module boundaries |
| PostgreSQL over SQL Server | Open-source, JSON support, RLS, cost-effective SaaS |
| Hangfire for jobs | Native .NET, PostgreSQL-backed, easy local dev |
| React Flow for diagrams | Mature, performant, customizable enterprise graphs |
| Abstract AI provider | Vendor flexibility, testability, cost optimization |
| Encrypted connector secrets | AES-256-GCM with per-tenant key derivation |
