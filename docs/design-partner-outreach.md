# Design Partner Outreach Playbook

Template for recruiting 3–5 design partners before general availability.

## Ideal Partner Profile

- 50–500 engineers, multiple repositories and databases
- Active change management pain (tickets, approvals, compliance)
- Willing to share feedback weekly for 8–12 weeks
- Has a technical champion (architect or eng manager) and a business sponsor

## Value Proposition (30 seconds)

StackPilot connects your code, databases, and CI/CD into a living knowledge graph. AI drafts requirements grounded in your actual architecture, routes them through configurable approval gates, and tracks changes from ticket to production—with full audit trails.

## Outreach Email Template

**Subject:** Early access: AI change intelligence for [Company] engineering

Hi [Name],

I'm building StackPilot—an enterprise platform that maps your software estate and automates the ticket → requirements → approval → implementation workflow with graph-grounded AI.

We're inviting a small group of design partners to shape the product before launch. In exchange for weekly feedback calls, you get:

- Free access during the pilot
- Direct influence on roadmap priorities
- White-glove onboarding and connector setup

Would you be open to a 20-minute demo next week?

Best,
[Your name]

## Discovery Call Questions

1. How do you document architecture today? (Confluence, wikis, tribal knowledge?)
2. What does your change approval process look like?
3. Where do AI tools help—or fail—today?
4. Which systems must we integrate in month one? (GitHub, Jira, databases?)
5. Who signs off on production releases?

## Success Metrics for Pilot

| Metric | Target |
|--------|--------|
| Time from ticket to approved requirements | < 2 days |
| Approval cycle time | < 24 hours |
| Connector sync reliability | > 95% |
| Weekly active users (champion team) | > 3 |
| NPS from champion | > 40 |

## Pilot Timeline

| Week | Focus |
|------|-------|
| 1 | Onboarding, connectors, first ticket |
| 2–3 | Requirements + approval workflow |
| 4–5 | QA/UAT, CI integration |
| 6–8 | Production release, retrospective |

## Follow-Up Cadence

- **Weekly:** 30-min sync with champion
- **Bi-weekly:** Written summary of bugs + requests
- **End of pilot:** Case study draft (with permission)

## Objection Handling

| Objection | Response |
|-----------|----------|
| "We already have Jira" | StackPilot syncs with Jira bidirectionally; we add architecture context Jira lacks |
| "AI isn't trustworthy" | Requirements include citations to your graph; humans approve every gate |
| "Security concerns" | RBAC, tenant isolation, PostgreSQL RLS, audit logs, on-prem option via Docker |

## Next Steps After Interest

1. Send [DEMO.md](../DEMO.md) and schedule technical demo
2. Sign lightweight pilot agreement (no fee, feedback commitment)
3. Provision org + schedule connector setup session
