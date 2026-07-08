# Release Rollback Plan (This Week)

## Scope of Recent Changes

- RLS enforcement and integration test hardening.
- E2E reliability updates for onboarding, invite, and release flow.
- Enterprise surface updates (SAML settings, compliance endpoints, connector workflow improvements).

## Rollback Triggers

Rollback if any of the following occur after release:

1. Tenant data visibility breach across organizations.
2. Login/invite/onboarding path is broken for new users.
3. Approval/QA/UAT/release flow is blocked for standard users.
4. ServiceNow sync creates duplicate or corrupt records.

## Rollback Strategy

## 1) Application Rollback

1. Re-deploy previous known-good container/image.
2. Verify health endpoint and auth/login first.
3. Run smoke checks:
   - dashboard load
   - invite acceptance
   - ticket progression path

## 2) Feature Rollback

If full rollback is too disruptive, disable impacted user paths first:

- Temporarily disable staging E2E gate if it is failing due to external infra only.
- Disable new connector paths in UI via org feature flags where applicable.

## 3) Data Rollback Guidance

Database migrations are additive and include RLS policy updates. If data rollback is needed:

1. Take fresh backup/snapshot before any rollback migration.
2. Use migration down scripts only after confirming impact and FK dependencies.
3. Prefer app rollback over schema rollback unless schema itself causes outage.

## Validation After Rollback

- CI backend/frontend checks are green on rollback commit.
- Manual checks:
  - login
  - onboarding
  - invite flow
  - approval/qa/uat/release flow
  - org data isolation sanity check

## Ownership

- Release manager: owns go/no-go and rollback decision.
- Backend owner: executes API/runtime rollback.
- Frontend owner: verifies user-path recovery.
- Data owner: approves any migration rollback.
