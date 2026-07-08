# Support Handoff and First-24h Monitoring

## What Support Should Watch

1. Login failures for newly registered users.
2. Invite acceptance issues (invalid token, redirect loops).
3. Ticket progression blockers (approval, QA, UAT actions not persisting).
4. Cross-organization data visibility reports (treat as P0).
5. Connector sync anomalies (duplicate ServiceNow-linked tickets).

## Triage Playbook

## A) Auth / Onboarding

- Confirm API health endpoint is healthy.
- Verify user exists and has organization membership.
- Check whether user is stuck on onboarding due to missing org/workspace.

## B) Invite Flow

- Validate invite token is unexpired.
- Confirm invitee email matches accepted account.
- Confirm org membership record created after acceptance.

## C) Ticket Workflow

- Verify user role permissions for approvals/qa/uat actions.
- Confirm status transitions follow state machine path.
- Check audit logs for denied action and missing permission.

## D) RLS / Data Isolation

- Immediately escalate any cross-org visibility report.
- Validate request headers contain expected organization context.
- Verify app DB role is non-superuser in deployed environment.

## First-24h Metrics

1. API success/error rate by endpoint family:
   - `/auth/*`
   - `/organizations/*`
   - `/tickets/*`
2. E2E synthetic checks:
   - login
   - invite accept
   - approval -> qa -> uat -> release
3. Connector health status counts.
4. Number of failed invite accept attempts.
5. Number of support tickets tagged `release-week`.

## Escalation Routing

- P0 (security/data isolation): Security + backend owner immediately.
- P1 (core flow down): On-call backend/frontend pair.
- P2 (partial degradation): queue for same-day patch.
