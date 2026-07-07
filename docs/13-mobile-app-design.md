# StackPilot — Mobile App Design

## Framework: React Native (Expo)

Mobile app focused on approvals, visibility, and executive workflows — not a coding environment.

## Target Users

- Executives (dashboards, risk summaries)
- UAT Approvers (accept/reject changes)
- QA Leads (quick sign-off review)
- Client Admins (approval routing)

## Core Screens

| Screen | Purpose |
|--------|---------|
| Home Dashboard | KPI cards, pending approvals count, risk alerts |
| Approval Queue | Swipeable approval cards with approve/reject |
| Ticket Summary | Read-only ticket detail with AI summary |
| Deployment Status | Live deployment progress |
| Risk Alerts | Push notification center |
| AI Assistant | Voice query interface |
| Architecture Health | Simplified health score visualization |
| Search | Global search across tickets, apps, docs |

## Navigation

Bottom tab bar:
- Home
- Approvals (badge count)
- Deployments
- Search
- Profile

## Push Notifications

| Event | Notification |
|-------|-------------|
| Approval requested | "Approval needed: Ticket #1234" |
| QA complete | "QA passed for Ticket #1234 — UAT needed" |
| Deployment started | "Production deployment in progress" |
| Risk alert | "High-risk change detected in Order API" |
| AI summary ready | "Weekly architecture health report available" |

## Voice Queries

Integration with device speech-to-text:
- "What tickets need my approval?"
- "Show deployment status for Order API"
- "What is our current risk score?"

## Offline Support

- Cache last 50 tickets and approval queue
- Queue approval decisions for sync
- Read-only documentation browsing

## Security

- Biometric authentication (Face ID / fingerprint)
- Short-lived tokens with refresh
- No credential storage on device
- Remote wipe capability via MDM

## Project Structure (Future)

```
mobile/
  app/              # Expo Router screens
  components/       # Mobile-optimized components
  services/         # API client, push notifications
  app.json
  package.json
```
