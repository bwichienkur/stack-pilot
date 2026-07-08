# StackPilot

**AI-powered Enterprise Software Intelligence Platform**

StackPilot connects to repositories, databases, and CI/CD systems to build a knowledge graph of your software ecosystem. It supports governed change workflows from business ticket through AI-generated requirements, approval, QA/UAT, and production release tracking.

## What works today

| Area | Status |
|------|--------|
| Multi-tenant orgs, workspaces, RBAC | Production-ready foundation |
| JWT auth + refresh tokens + OIDC SSO + SAML 2.0 (Sustainsys.Saml2) | Implemented |
| **13 categorized connectors** (see table below) | Test + sync flows implemented |
| Repository & database scans → knowledge graph | End-to-end via Hangfire workers |
| Jira + ServiceNow ticket sync | Import + status push-back on linked tickets |
| RAG-grounded AI requirements + approval gates | Implemented |
| QA / UAT queues + release calendar (`/releases`) | Implemented |
| GitHub Actions webhooks → build runs + PR↔ticket linking | Implemented |
| pgvector RAG (PostgreSQL) | Native cosine search; JSON fallback in tests |
| Slack notifications (org webhook in Settings) | Implemented |
| Redis caching, OpenTelemetry 1.15.3, audit logs | Implemented |
| Docker Compose (pgvector Postgres + API + Workers + Frontend) | Implemented |
| Playwright golden-path E2E in CI | Implemented |

### Connectors by category

| Category | Connectors |
|----------|------------|
| **Source code** | GitHub Repository, GitLab Repository, Azure DevOps Repos, Bitbucket Repository |
| **Data** | SQL Server, PostgreSQL, MySQL, MongoDB |
| **CI/CD** | GitHub Actions, Azure Pipelines, Jenkins |
| **ITSM & work** | Jira, ServiceNow |

Deep code scanning runs today for **GitHub** and **GitLab**; **Azure DevOps** and **Bitbucket** detect frameworks via `package.json` / `.sln`. Database connectors run schema/collection scans.

## What is preview or scaffolded

| Area | Status |
|------|--------|
| SAML 2.0 SSO | Production ACS via Sustainsys.Saml2; per-org IdP config in Settings (Professional+) |
| Desktop (Tauri) / Mobile (Expo) | Scaffolds with README |
| AI code generation | Provider-backed codegen + governed git/PR workflow actions |
| Billing, teams UI, connector marketplace | Pricing page + Stripe Checkout; limit enforcement + marketplace search |
| Kanban drag-and-drop | Drag tickets between workflow columns |

## Quick Start

### Docker (recommended)

```bash
docker compose up --build
# Frontend http://localhost:3000 · API http://localhost:5000
```

Migrations run automatically on API startup.

### Demo environment (design partners)

```bash
docker compose up -d
DEMO_SEED=true dotnet run --project src/StackPilot.Api
cd frontend && npm run dev
```

Login: `demo@stackpilot.dev` / `DemoPassword123!` — see [DEMO.md](DEMO.md) for the walkthrough.

### Local development

```bash
docker compose up -d postgres redis
cd src/StackPilot.Api && dotnet run          # :5000
cd src/StackPilot.Workers && dotnet run      # background jobs
cd frontend && npm install && npm run dev    # :3000
```

See [docs/ops-runbook.md](docs/ops-runbook.md) for environment variables, health checks, and troubleshooting.

## Architecture

- **Backend:** .NET 8 Clean Architecture (`Api`, `Application`, `Domain`, `Infrastructure`, `Workers`)
- **Frontend:** Next.js 14, TypeScript, Tailwind CSS, React Flow, React Query
- **Database:** PostgreSQL + pgvector + EF Core (tenant filters + RLS)
- **Queue:** Hangfire (Workers host in production)
- **Cache:** Redis with in-memory fallback
- **AI:** Mock / OpenAI / Anthropic via composite provider

## Testing

```bash
dotnet test tests/StackPilot.IntegrationTests
cd frontend && npm run test:e2e   # requires API + frontend running
```

CI runs backend tests, frontend build, and Playwright golden-path E2E on every push.

## Documentation

| Doc | Purpose |
|-----|---------|
| [DEMO.md](DEMO.md) | Repeatable 5-minute demo script |
| [docs/14-billing-pricing.md](docs/14-billing-pricing.md) | Pricing tiers, Stripe setup, design-partner conversion |
| [docs/design-partner-outreach.md](docs/design-partner-outreach.md) | Pilot recruitment + first-call playbook |
| [docs/ops-runbook.md](docs/ops-runbook.md) | Operations and troubleshooting |
| `/docs` planning files | Architecture and product specs |

## Security

- JWT + RBAC on all protected API routes
- Tenant isolation via `X-Organization-Id`, EF global filters, PostgreSQL RLS (API + workers)
- Connector credentials encrypted (AES-256-GCM)
- Configurable approval gates before implementation
- AI outputs require human approval before implementation plans
- Audit logging for mutations

## License

Proprietary — All rights reserved.
