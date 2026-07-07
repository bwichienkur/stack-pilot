# StackPilot Demo Script

Repeatable demo for design partners and investor walkthroughs.

## Prerequisites

```bash
docker compose up -d
export DEMO_SEED=true
cd src/StackPilot.Api && dotnet run
# separate terminal
cd frontend && npm run dev
```

## Demo Credentials

| Field | Value |
|-------|-------|
| Email | `demo@stackpilot.dev` |
| Password | `DemoPassword123!` |
| Org | Acme Corp |
| Workspace | Production |

## 5-Minute Walkthrough

### 1. Dashboard (30s)
- Log in with demo credentials
- Show KPI cards: applications, connectors, open tickets, pending approvals

### 2. Architecture Map (45s)
- Navigate to **Architecture**
- Highlight **Customer Portal** application node from seeded graph data

### 3. Ticket Workflow (2 min)
- Open ticket **#1 — Add two-factor authentication**
- **Requirements** tab: show AI-generated requirements with graph citations
- **Impact** tab: impacted nodes from citation analysis
- **Approvals** tab: approve from ticket detail or **Approval Queue**

### 4. Implementation & CI (1 min)
- After approval, generate implementation plan
- Show **Builds** tab (explain PR linking via `feature/{ticketNumber}` branch naming)
- **Deployments** page for build run history

### 5. QA → UAT → Release (1 min)
- Move ticket to test (or use QA queue with `DeployedToTest` status)
- **QA Queue**: pass QA
- **UAT Queue**: accept UAT
- Schedule release from ticket detail

### 6. AI Copilot & Docs (30s)
- Open command palette (`Cmd+K`)
- **Documentation Hub**: generate AI documentation for seeded page

## Talking Points

- **Honest scope**: mock AI provider works offline; swap to OpenAI/Anthropic via env vars
- **Security**: RBAC, tenant isolation, PostgreSQL RLS, audit trail
- **Integrations**: GitHub, GitLab, Jira (bidirectional ticket sync), SQL Server, PostgreSQL

## Reset Demo Data

```bash
docker compose down -v
docker compose up -d
DEMO_SEED=true dotnet run --project src/StackPilot.Api
```
