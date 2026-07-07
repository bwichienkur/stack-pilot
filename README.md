# StackPilot

**AI-powered Enterprise Software Intelligence Platform**

StackPilot connects to your repositories, databases, CI/CD systems, and cloud environments to build a living knowledge graph of your software ecosystem. It enables documentation generation, architecture visualization, AI-driven recommendations, and governed change workflows from business ticket to production deployment.

## Architecture

- **Backend:** .NET 8 Clean Architecture modular monolith
- **Frontend:** Next.js 14, TypeScript, Tailwind CSS, React Flow
- **Database:** PostgreSQL with EF Core
- **Queue:** Hangfire (PostgreSQL-backed)
- **Cache:** Redis
- **AI:** Abstract provider interface with mock/OpenAI/Anthropic support

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js 18+
- Docker (for PostgreSQL and Redis)

### 1. Start Infrastructure

```bash
docker compose up -d
```

### 2. Run the API

```bash
cd src/StackPilot.Api
dotnet run
```

API: http://localhost:5000
Swagger: http://localhost:5000/swagger
Hangfire Dashboard: http://localhost:5000/hangfire

### 3. Run the Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend: http://localhost:3000

## Project Structure

```
StackPilot/
├── docs/                    # Planning & design documents
├── src/
│   ├── StackPilot.Domain/         # Entities, enums, domain logic
│   ├── StackPilot.Application/   # DTOs, interfaces, connector contracts
│   ├── StackPilot.Infrastructure/ # EF Core, services, connectors, jobs
│   ├── StackPilot.Api/           # REST API, middleware, auth
│   └── StackPilot.Workers/       # Background worker host
├── frontend/                # Next.js web application
└── docker-compose.yml       # PostgreSQL + Redis
```

## MVP Features

- Multi-tenant SaaS foundation (orgs, workspaces, users, RBAC)
- JWT authentication with audit logging
- Connector framework (GitHub, SQL Server, PostgreSQL, GitHub Actions)
- Repository and database scanning
- Knowledge graph with impact analysis
- Interactive architecture map (React Flow)
- Ticketing system with full lifecycle
- AI requirements and implementation plan generation
- Approval workflow (technical, security, QA, UAT, release)
- QA evidence and UAT decision flows
- Production release scheduling
- AI assistant with governance and audit trail
- Premium dark-mode enterprise UI

## Documentation

See the `/docs` folder for:

1. [Product Requirements](docs/01-product-requirements.md)
2. [Architecture](docs/02-architecture.md)
3. [Module Breakdown](docs/03-module-breakdown.md)
4. [Database Schema](docs/04-database-schema.md)
5. [API Design](docs/05-api-design.md)
6. [AI Workflow Design](docs/06-ai-workflow-design.md)
7. [Connector Design](docs/07-connector-design.md)
8. [Background Job Design](docs/08-background-job-design.md)
9. [Security Model](docs/09-security-model.md)
10. [UI Page Map](docs/10-ui-page-map.md)
11. [MVP Roadmap](docs/11-mvp-roadmap.md)
12. [Desktop App Design](docs/12-desktop-app-design.md)
13. [Mobile App Design](docs/13-mobile-app-design.md)

## Security

- All API endpoints require JWT authentication
- Tenant isolation via EF Core global query filters
- Connector credentials encrypted with AES-256-GCM
- AI actions fully audited with approval gates
- No AI direct access to production

## License

Proprietary — All rights reserved.
