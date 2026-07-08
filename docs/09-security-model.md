# StackPilot — Security Model

## 1. Authentication

| Method | Status | Description |
|--------|--------|-------------|
| Email/Password | MVP | ASP.NET Identity with bcrypt hashing |
| JWT Bearer | MVP | Access token (15min) + refresh token (7d) |
| OIDC | Architecture ready | External identity provider integration |
| SAML 2.0 | Architecture ready | Enterprise SSO |
| API Keys | Post-MVP | Service-to-service authentication |

## 2. Authorization (RBAC)

### System Roles (seeded)

| Role | Key Permissions |
|------|----------------|
| Platform Super Admin | `*` (all) |
| Client Admin | `org:*`, `connectors:*`, `settings:*`, `users:*` |
| Architect | `graph:*`, `docs:*`, `tickets:approve:technical`, `recommendations:*` |
| Developer | `tickets:*`, `repos:read`, `graph:read`, `docs:read` |
| QA | `tickets:qa`, `tickets:read` |
| UAT Approver | `tickets:uat`, `tickets:read` |
| Business Requester | `tickets:create`, `tickets:read:own` |
| Read-only Executive | `graph:read`, `docs:read`, `tickets:read`, `dashboard:read` |

### Permission Format

`{resource}:{action}[:{scope}]`

Examples: `tickets:create`, `tickets:approve:security`, `connectors:manage`

### Enforcement

- ASP.NET authorization policies per permission
- EF Core global query filters for tenant isolation
- Workspace-level scoping in service layer
- API middleware validates `X-Organization-Id` matches JWT claims
- `X-Workspace-Id` must belong to the selected organization (403 otherwise)

## 3. Data Protection

| Data | Protection |
|------|------------|
| Passwords | bcrypt (work factor 12) |
| Connector credentials | AES-256-GCM, per-org key derivation |
| JWT secrets | Environment variable / Azure Key Vault |
| API keys | SHA-256 hashed, prefix-only display |
| Audit logs | Append-only, no UPDATE/DELETE |
| PII | Encrypted at rest, minimal collection |

## 4. Network Security

- TLS 1.2+ for all communications
- CORS restricted to known origins
- Rate limiting per IP and per user
- Request size limits
- SQL injection prevention via parameterized queries (EF Core)
- XSS prevention via output encoding

## 5. Tenant Isolation

```
Request → JWT validation → Extract org_id claim
        → Middleware sets ITenantContext
        → EF Core global filter: WHERE organization_id = @currentOrgId
        → Service layer validates workspace access
        → Response (never leaks cross-tenant data)
```

Defense in depth:
1. JWT claim validation
2. Middleware tenant context
3. EF Core query filters
4. Service-layer authorization checks
5. PostgreSQL row-level security (production)

## 6. AI Safety

- No direct production access for AI agents
- All code changes via PR workflow
- Approval gates before any write action
- Full audit trail of AI decisions
- Token budget limits per organization
- Prompt injection mitigation via input sanitization

## 7. Audit Requirements

All auditable events:
- Authentication (login, logout, failed attempts)
- Authorization failures
- CRUD on sensitive entities
- Connector credential access
- AI action execution
- Approval decisions
- Deployment actions
- Settings changes
- User/role management

Audit log fields: timestamp, user, action, entity, details, IP, user agent.

Retention: 2 years minimum, configurable per plan.

## 8. Compliance Readiness

| Control | Implementation |
|---------|----------------|
| SOC 2 Type II | Audit logs, access controls, encryption |
| GDPR | Data export, deletion, consent |
| Data residency | Configurable DB region (post-MVP) |
| Penetration testing | Annual third-party assessment |

## 9. Secrets Management

Development: User secrets / `.env` files (never committed)
Production: Azure Key Vault / AWS Secrets Manager / HashiCorp Vault

Master encryption key rotation procedure documented in ops runbook.
