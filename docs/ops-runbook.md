# StackPilot Operations Runbook

## Architecture

| Service | Port | Role |
|---------|------|------|
| `StackPilot.Api` | 5000 | REST API, Hangfire dashboard (enqueue only in production) |
| `StackPilot.Workers` | — | Hangfire job processor (connector sync, scans, AI jobs) |
| `frontend` | 3000 | Next.js web UI |
| `postgres` | 5432 | Primary database |
| `redis` | 6379 | Response cache (connector definitions, dashboard stats) |

## Quick start (Docker)

```bash
docker compose up --build
```

Apply EF migrations on first deploy:

```bash
dotnet ef database update --project src/StackPilot.Infrastructure --startup-project src/StackPilot.Api
```

## Local development (without Docker)

```bash
docker compose up -d postgres redis
cd src/StackPilot.Api && dotnet run          # API on :5000
cd src/StackPilot.Workers && dotnet run      # Background jobs
cd frontend && npm run dev                   # UI on :3000
```

In Development, `Hangfire:RunServerOnApi` defaults to `true` so jobs run without the Workers host. In production/Docker, set it to `false` and run Workers separately.

## Health checks

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Liveness — API process up |
| `GET /health/ready` | Readiness — PostgreSQL (+ Redis when configured) |

## Required environment variables (production)

| Variable | Description |
|----------|-------------|
| `STACKPILOT_JWT_KEY` | JWT signing key (min 32 chars) |
| `STACKPILOT_ENCRYPTION_KEY` | Connector credential encryption |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `ConnectionStrings__Redis` | Redis connection string (optional; falls back to in-memory cache) |
| `Hangfire__DashboardApiKey` | API key for `/hangfire` dashboard (non-Development) |
| `Cors__Origins__0` | Allowed frontend origin |

## Hangfire

- **Dashboard:** `http://localhost:5000/hangfire`
- **Queues:** `critical`, `default`, `low`
- **Jobs:** connector sync, repository scan, database scan, generate requirements

If jobs stay queued, confirm `StackPilot.Workers` is running and shares the same PostgreSQL connection as the API.

## GitHub webhooks

Point repository webhooks to:

```
POST https://<api-host>/api/v1/webhooks/github
```

Subscribe to **Workflow runs** events. Build runs appear on the Deployments page when a matching `github_actions` connector exists for the repository.

## Observability

- **Logs:** Serilog to stdout (structured JSON in production)
- **Traces:** OpenTelemetry via `OpenTelemetry:OtlpEndpoint` (Jaeger, Grafana Tempo, etc.)
- **Custom spans:** `ai.chat`, `ai.generate_requirements`, `job.connector_sync`, `job.repository_scan`, `job.database_scan`, `job.generate_requirements`

## API versioning

Current version: **v1** (`/api/v1/...`). Response headers:

- `X-API-Version: 1.0`
- `X-API-Supported-Versions: 1.0`

## Troubleshooting

| Symptom | Check |
|---------|-------|
| 403 on API calls | `X-Organization-Id` header; user org membership; RBAC permissions |
| Jobs never complete | Workers service running; Hangfire tables in PostgreSQL |
| Session expires quickly | Refresh token flow; `POST /api/v1/auth/refresh` |
| Redis connection errors | Redis host reachable; API falls back to in-memory cache if Redis unset |
| SSO redirect issues | Token delivered via URL hash `#access_token=` not query string |

## CI/CD

GitHub Actions workflow `.github/workflows/ci.yml` runs on push/PR:

- `dotnet build` + integration tests
- `npm run build` (frontend)
