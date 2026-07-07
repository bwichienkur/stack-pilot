# StackPilot

**AI-powered Enterprise Software Intelligence Platform**

StackPilot connects to repositories, databases, and CI/CD systems to build a knowledge graph of your software ecosystem. It supports governed change workflows from business ticket through AI-generated requirements to approval and deployment tracking.

## What works today

| Area | Status |
|------|--------|
| Multi-tenant orgs, workspaces, RBAC | Production-ready foundation |
| JWT auth + refresh tokens + OIDC SSO scaffold | Implemented |
| GitHub / GitLab / PostgreSQL / SQL Server connectors | Real integrations |
| Jira connector | Project sync + connection test |
| Repository & database scans → knowledge graph | End-to-end via Hangfire workers |
| RAG-grounded AI requirements + approval queue | Implemented |
| GitHub Actions webhooks → build runs | Implemented |
| Slack notifications (org webhook in Settings) | Implemented |
| Redis caching, OpenTelemetry, audit logs | Implemented |
| Docker Compose (API + Workers + Frontend + Postgres + Redis) | Implemented |

## What is preview or scaffolded

| Area | Status |
|------|--------|
| QA / UAT queues | UI stubs (backend APIs exist) |
| Desktop (Tauri) / Mobile (Expo) | Scaffolds only |
| Full Jira bidirectional ticket sync | Not yet implemented |
| Native pgvector queries | Embeddings stored as JSON today |
| PostgreSQL RLS | Policies added; defense-in-depth with EF filters |

## Quick Start

### Docker (recommended)

```bash
docker compose up --build
# Frontend http://localhost:3000 · API http://localhost:5000
```

Apply migrations on first run:

```bash
dotnet ef database update --project src/StackPilot.Infrastructure --startup-project src/StackPilot.Api
```

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
- **Frontend:** Next.js 14, TypeScript, Tailwind CSS, React Flow
- **Database:** PostgreSQL + EF Core (tenant filters + optional RLS)
- **Queue:** Hangfire (Workers host in production)
- **Cache:** Redis with in-memory fallback
- **AI:** Mock / OpenAI / Anthropic via composite provider

## Testing

```bash
dotnet test tests/StackPilot.IntegrationTests
cd frontend && npm run test:e2e   # requires API + frontend running
```

## Documentation

Planning docs live in `/docs`. Operational runbook: [docs/ops-runbook.md](docs/ops-runbook.md).

## Security

- JWT + RBAC on all protected API routes
- Tenant isolation via `X-Organization-Id`, EF global filters, and PostgreSQL RLS policies
- Connector credentials encrypted (AES-256-GCM)
- AI outputs require human approval before implementation plans
- Audit logging for mutations

## License

Proprietary — All rights reserved.
