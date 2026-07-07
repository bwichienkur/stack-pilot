# StackPilot — MVP Roadmap & Implementation Plan

## Status: Post-Review (July 2026)

The 100-task independent review backlog is **complete**. Post-review completion items (E2E golden path, QA/UAT queues, pgvector RAG, PR linking, SAML scaffold, etc.) are **implemented**.

## Phase 1: Foundation ✅

- [x] Clean Architecture solution scaffold
- [x] Domain entities and EF Core DbContext
- [x] Multi-tenancy with global query filters + PostgreSQL RLS
- [x] RBAC with seeded roles/permissions
- [x] JWT authentication + refresh token rotation
- [x] OIDC SSO scaffold + SAML 2.0 metadata scaffold
- [x] Audit logging middleware
- [x] API with OpenAPI/Swagger

## Phase 2: Connectors & Intelligence ✅

- [x] Connector framework (registry, encryption, sync jobs)
- [x] GitHub, GitLab, SQL Server, PostgreSQL, Jira connectors
- [x] GitHub Actions CI/CD tracking + webhooks
- [x] Repository and database scanners
- [x] Hangfire background jobs (API + Workers host)
- [x] Redis caching

## Phase 3: Knowledge Graph & Documentation ✅

- [x] Graph nodes/edges, search, impact analysis
- [x] Auto-populate from scans
- [x] Documentation versioning
- [x] AI documentation generator (UI + API)
- [x] pgvector RAG index (PostgreSQL) with JSON fallback for tests

## Phase 4: Workflow & AI ✅

- [x] Full ticket lifecycle (intake → production)
- [x] AI requirements with graph citations
- [x] Configurable approval gates engine
- [x] AI implementation plan generator
- [x] QA queue + UAT queue (API + UI)
- [x] Production release scheduling
- [x] AI action audit trail + governance

## Phase 5: CI/CD Integration ✅

- [x] GitHub Actions build tracking
- [x] PR ↔ ticket linking (branch naming + webhooks)
- [x] Build runs on ticket detail

## Phase 6: Frontend ✅

- [x] Next.js dark mode UI (shadcn-style components)
- [x] Auth, onboarding, dashboard
- [x] Connectors, architecture map, applications
- [x] Ticket board + full detail workflow tabs
- [x] Approval, QA, UAT queues
- [x] AI copilot + command palette
- [x] React Query for key data pages
- [x] Playwright E2E golden path in CI

## Phase 7: Polish & Deploy ✅

- [x] Docker Compose (pgvector PostgreSQL, Redis, API, workers, frontend)
- [x] Demo seed data (`DEMO_SEED=true`, see [DEMO.md](../DEMO.md))
- [x] Integration tests (auth, tickets, QA/UAT, golden path, tenant isolation)
- [x] OpenTelemetry 1.15.3 (NU1902 advisory resolved)
- [x] Staging deploy workflow
- [x] Ops runbook

## Post-Roadmap Improvements ✅ (July 2026)

- [x] Public `/pricing`, GitHub webhook HMAC, approval gates API + Settings UI
- [x] Schedule release UI, ticket state machine, release deploy/verify/rollback
- [x] Member invites + seat limits, mobile nav drawer, docs viewer
- [x] Executive dashboard widgets, React Query hooks, error boundary
- [x] SAML plan gating + dev mode, Azure DevOps/Bitbucket scanners, Jira push-back
- [x] Audit retention job, GDPR export/delete API, extended RLS, admin console
- [x] Outbound webhooks, AI code scaffold, AI overage tracking, connector SDK docs

## Scaffold / Next Production Hardening

| Item | Status |
|------|--------|
| SAML full IdP integration | Dev mode + plan gating; production IdP config in Settings |
| Desktop app (Tauri) | Scaffold — loads web frontend |
| Mobile app (Expo) | Approvals queue read-only view |
| Billing | Phase 1+2 shipped (enforcement, portal, DESIGNPARTNER20) |
| Production K8s manifests | Use Docker Compose + staging workflow |
| SOC 2 Type II | Checklist in [16-soc2-readiness.md](16-soc2-readiness.md) |

## Post-MVP Roadmap

| Quarter | Focus |
|---------|-------|
| Q1 | Design partner pilots, SAML production, additional connectors |
| Q2 | Tauri desktop polish, advanced recommendations |
| Q3 | Mobile approvals GA, billing integration |
| Q4 | Enterprise compliance (SOC2), multi-region |
