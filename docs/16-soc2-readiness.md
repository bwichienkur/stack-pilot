# SOC 2 Readiness Checklist

StackPilot implements technical controls aligned with SOC 2 Trust Services Criteria. Formal Type II certification is a Q4 roadmap item.

## Implemented controls

| Control | Implementation |
|---------|----------------|
| Access control | JWT + RBAC, 27 permissions, tenant isolation |
| Audit logging | Append-only `AuditLog`, middleware + service events |
| Encryption at rest | AES-256-GCM connector credentials |
| Encryption in transit | HTTPS (deployment responsibility) |
| Password policy | bcrypt work factor 12 |
| Rate limiting | Auth and AI endpoints |
| Change management | Ticket workflow state machine, approval gates |
| Data retention | `DataRetentionJob` purges audit logs per plan tier |
| GDPR export/delete | `POST /organizations/{id}/export` and `POST /organizations/{id}/delete-data` APIs + Settings UI |

## Gaps for certification

- Formal policies (access review, incident response, vendor management)
- Penetration test report
- Employee security training records
- SIEM integration and alerting runbooks

## Evidence collection

- Export audit logs: `GET /api/v1/organizations/{id}/audit-logs`
- Org data export: `POST /api/v1/organizations/{id}/export` (Client Admin)
