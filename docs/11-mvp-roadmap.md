# StackPilot — MVP Roadmap & Implementation Plan

## MVP Scope Summary

Build a working foundation demonstrating all core workflows end-to-end with production-quality architecture.

## Phase 1: Foundation (Current)

- [x] Planning documentation
- [ ] Solution scaffold (Clean Architecture)
- [ ] Domain entities and EF Core DbContext
- [ ] Multi-tenancy with global query filters
- [ ] RBAC with seeded roles/permissions
- [ ] JWT authentication
- [ ] Audit logging middleware
- [ ] API project with OpenAPI

## Phase 2: Connectors & Intelligence

- [ ] Connector framework (registry, interface, credential encryption)
- [ ] GitHub repository connector
- [ ] SQL Server connector
- [ ] PostgreSQL connector
- [ ] GitHub Actions connector
- [ ] Repository scanner service
- [ ] Database scanner service
- [ ] Background sync jobs (Hangfire)

## Phase 3: Knowledge Graph & Documentation

- [ ] Graph node/edge CRUD and search
- [ ] Auto-populate graph from scans
- [ ] Impact analysis service
- [ ] Documentation model with versioning
- [ ] AI documentation generator (mock + real provider)

## Phase 4: Workflow & AI

- [ ] Ticket CRUD with full status lifecycle
- [ ] AI requirements generator
- [ ] Approval workflow engine
- [ ] AI implementation plan generator
- [ ] QA evidence and UAT decision flows
- [ ] Production release scheduling
- [ ] AI action audit trail

## Phase 5: CI/CD Integration

- [ ] GitHub Actions build tracking
- [ ] PR linking to tickets
- [ ] Build result attachment

## Phase 6: Frontend

- [ ] Next.js app with dark mode
- [ ] shadcn/ui component library
- [ ] Auth pages (login, onboarding)
- [ ] Dashboard with KPI cards
- [ ] Connector management pages
- [ ] Architecture map (React Flow)
- [ ] Ticket board and detail
- [ ] Approval, QA, UAT queues
- [ ] AI copilot panel
- [ ] Command palette

## Phase 7: Polish & Deploy

- [ ] Docker Compose for local dev
- [ ] Seed data script
- [ ] Integration tests for critical paths
- [ ] OpenTelemetry configuration
- [ ] README with setup instructions

## Post-MVP Roadmap

| Quarter | Focus |
|---------|-------|
| Q1 | SSO (OIDC/SAML), additional connectors (GitLab, Jira) |
| Q2 | Desktop app (Tauri), advanced recommendations |
| Q3 | Mobile app (approvals/dashboards), billing integration |
| Q4 | Service extraction, graph DB migration, marketplace |

## Implementation Principles

1. **Security first** — Every endpoint authenticated, every query tenant-scoped
2. **Audit everything** — AI actions, approvals, credential access
3. **Approval gates** — No AI write without human approval
4. **Idempotent jobs** — Safe retries for all background work
5. **Clean boundaries** — Modules communicate via interfaces and events
6. **Test critical paths** — Auth, tenancy isolation, approval workflow

## Local Development Setup

```bash
# Start infrastructure
docker compose up -d

# Backend
cd src/StackPilot.Api
dotnet run

# Frontend
cd frontend
npm install && npm run dev

# Workers
cd src/StackPilot.Workers
dotnet run
```

Services:
- API: http://localhost:5000
- Frontend: http://localhost:3000
- PostgreSQL: localhost:5432
- Redis: localhost:6379
- Hangfire Dashboard: http://localhost:5000/hangfire
