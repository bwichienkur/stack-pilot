# Release Notes Draft (This Week)

## Highlights

- Improved end-to-end reliability for onboarding, invite acceptance, and ticket workflow.
- Hardened tenant isolation verification with Postgres RLS-focused test updates.
- Enhanced enterprise readiness across SAML and compliance-oriented surfaces.
- CI now validates backend, frontend, and E2E flows more consistently.

## User-Facing Improvements

1. Invite flow now behaves consistently for new users entering onboarding.
2. Approval -> QA -> UAT -> release journey is covered by reliable E2E tests.
3. Pricing page and core navigation paths are included in smoke coverage.

## Security & Compliance

1. Tenant RLS validation tightened in integration tests.
2. SAML settings API coverage and SOC2 readiness docs aligned.
3. GDPR data export/delete capabilities remain in release scope.

## Integrations

- Connector workflows (including enterprise connector paths) remain available.
- ServiceNow bidirectional behavior requires final staging credentialed smoke test before announcing broadly.

## Known Limitations / Follow-Ups

1. Staging deploy workflow still requires repo variables:
   - `STAGING_FRONTEND_URL`
   - `STAGING_API_URL`
2. Final staging manual validation is pending variable setup.
3. One stale draft PR is still open and should be explicitly closed or merged after owner review.

## Internal Notes

- PR #17 (E2E hardening helpers) is green and merge-ready.
- Main CI run after E2E fixes is green.
