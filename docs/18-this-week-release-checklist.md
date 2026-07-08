# This Week Release Checklist

Last updated: 2026-07-08

## 1) Release Gate

- [x] CI green on main (backend + frontend + e2e)
- [ ] Staging deploy green
- [ ] No open P0/P1 bugs (manual triage required)
- [x] Rollback plan documented (`docs/19-release-rollback-plan.md`)
- [x] Release notes drafted (`docs/20-release-notes-draft.md`)

## 2) Environment & Pipeline

- [ ] Set repo variables: `STAGING_FRONTEND_URL`, `STAGING_API_URL` (manual GitHub settings action required)
- [ ] Run `Deploy Staging` successfully (blocked by missing vars)
- [ ] Confirm `staging-e2e` runs (blocked by missing vars)
- [x] CI evidence captured (successful run: 28926709318; PR #17 checks green)

## 3) Product Critical Path (Staging)

Automated evidence from CI e2e on main:

- [x] Auth + onboarding
- [x] Invite accept flow
- [x] Approval -> QA -> UAT -> Releases flow
- [x] Pricing page smoke
- [ ] Staging manual confirmation with staging URLs (blocked by missing vars)

## 4) Enterprise Readiness

- [x] Tenant isolation regression covered in CI integration tests
- [x] SAML settings API coverage present in integration tests
- [ ] SAML staging smoke (manual, requires staging URLs)
- [ ] GDPR export/delete manual staging proof run

## 5) Integrations

- [ ] ServiceNow bidirectional staging smoke test (manual environment validation pending)

## 6) Code / PR Hygiene

- [x] E2E hardening PR #17 is green and merge-ready
- [x] Stale draft PR discovery completed (`#4` is still open)
- [ ] Close obsolete draft PRs (manual decision/action)

## 7) Launch Comms Pack

- [x] Draft release notes (`docs/20-release-notes-draft.md`)
- [x] Rollback plan (`docs/19-release-rollback-plan.md`)
- [x] Support handoff + first-24h monitoring (`docs/21-support-handoff-and-monitoring.md`)

## Current Blockers

1. Missing GitHub repo variables for staging URLs.
2. Remaining items requiring manual staging validation and production-like connector credentials.
