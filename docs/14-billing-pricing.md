# StackPilot — Billing & Pricing

## Pricing tiers (published)

| Plan | Monthly | Annual (save ~17%) | Included seats | Extra seat | Workspaces | Connectors | AI tokens / mo | Audit retention |
|------|---------|-------------------|----------------|------------|------------|------------|----------------|-----------------|
| **Trial** | Free | — | 5 (max 5) | — | 1 | 3 | 100K | 30 days |
| **Starter** | **$349** | **$3,490** | 10 (max 25) | $29 / seat | 2 | 8 | 1M | 90 days |
| **Professional** | **$899** | **$8,990** | 25 (max 100) | $35 / seat | 10 | 50 | 5M | 1 year |
| **Enterprise** | Custom | Custom annual | 100+ | Contract | Unlimited | Unlimited | 50M+ | 2+ years |

All paid plans include: full ticket workflow, approval gates, QA/UAT, release calendar, 13 connector types, knowledge graph, and AI requirements with citations.

**Professional** adds SAML SSO. **Enterprise** adds priority support, security review, custom data residency, and on-prem/VPC deployment.

---

## Trial

- **14 days** from organization creation (`TrialEndsAt` on the org record).
- Full product access within Trial limits (see table).
- No credit card required.
- After trial: org remains readable; new AI actions, connectors beyond limit, and seat invites should be blocked (enforcement phase 2).

---

## Design partner → paid conversion

| Offer | Detail |
|-------|--------|
| Pilot price | **Free** for 8–12 weeks (see [design-partner-outreach.md](design-partner-outreach.md)) |
| Conversion discount | **20% off year 1** if signed within 30 days of pilot end |
| Recommended landing plan | **Professional** for teams with Jira/ServiceNow + compliance needs |
| Upgrade path | Starter for single-team pilots → Professional at multi-workspace rollout |

### Conversion playbook

1. **Week 6** — Review usage dashboard (seats, connectors, AI tokens) with champion.
2. **Week 8** — Send pricing one-pager + recommended tier based on usage.
3. **Pilot end** — Offer 30-day conversion window with `DESIGNPARTNER20` (configure in Stripe).
4. **Checkout** — Client Admin uses Settings → Billing or `/pricing` → Stripe Checkout.
5. **Webhook** — `checkout.session.completed` sets `Plan`, `SubscriptionStatus`, `StripeSubscriptionId`.

---

## Metering & limits

Limits are defined in code: `PlanCatalog` (`src/StackPilot.Application/Billing/PlanCatalog.cs`).

| Dimension | How measured |
|-----------|----------------|
| Seats | `OrganizationMembers` count |
| Workspaces | `Workspaces` per org |
| Connectors | `ConnectorInstances` per org |
| AI tokens | Sum of `AiActions.TokensUsed` in current UTC month |

**Phase 1 (shipped):** limits exposed in billing API and UI; usage visible in Settings.  
**Phase 2 (next):** hard enforcement on connector create, member invite, and AI calls when over limit.

---

## Stripe integration

### Configuration (`appsettings` or environment)

```json
{
  "Billing": {
    "Stripe": {
      "SecretKey": "sk_live_...",
      "WebhookSecret": "whsec_...",
      "Prices": {
        "Starter": {
          "MonthlyPriceId": "price_starter_monthly",
          "AnnualPriceId": "price_starter_annual"
        },
        "Professional": {
          "MonthlyPriceId": "price_professional_monthly",
          "AnnualPriceId": "price_professional_annual"
        }
      }
    }
  }
}
```

| Variable | Purpose |
|----------|---------|
| `Billing__Stripe__SecretKey` | Stripe API key |
| `Billing__Stripe__WebhookSecret` | Webhook signature verification |
| `Billing__Stripe__Prices__{Plan}__MonthlyPriceId` | Checkout line item |
| `Billing__Stripe__Prices__{Plan}__AnnualPriceId` | Annual checkout |

When Stripe is **not** configured, checkout returns a **mock session** URL (for local dev and demos).

### API endpoints

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/api/v1/billing/plans` | Public | Published pricing catalog |
| GET | `/api/v1/billing/organizations/{id}` | Settings manage | Current plan, usage, trial |
| POST | `/api/v1/billing/organizations/{id}/checkout` | Settings manage | Create Stripe Checkout session |
| POST | `/api/v1/billing/webhooks/stripe` | Stripe signature | Subscription lifecycle |

### Webhook events handled

- `checkout.session.completed` — activate plan, store subscription ID
- `customer.subscription.updated` — sync status (active, past_due, canceled)
- `customer.subscription.deleted` — mark canceled

### Stripe Dashboard setup

1. Create Products: **StackPilot Starter**, **StackPilot Professional**.
2. Create Prices: monthly + annual for each (USD).
3. Add metadata to subscription: `organization_id`, `plan`.
4. Configure webhook endpoint: `https://{api-host}/api/v1/billing/webhooks/stripe`.
5. Enable Customer Portal for self-serve invoice history (optional).

---

## Data model

`Organization` fields:

| Field | Type | Purpose |
|-------|------|---------|
| `Plan` | enum | Trial / Starter / Professional / Enterprise |
| `SubscriptionStatus` | enum | Trialing / Active / PastDue / Canceled / Paused |
| `TrialEndsAt` | datetime | Trial expiry |
| `StripeCustomerId` | string | Stripe customer |
| `StripeSubscriptionId` | string | Active subscription |
| `BillingEmail` | string | Invoice contact |

---

## UI surfaces

| Surface | Path | Audience |
|---------|------|----------|
| Public pricing | `/pricing` | Prospects, design partners |
| Org billing | Settings → Billing card | Client Admins |
| Command palette | “Pricing” / “Billing” | Authenticated users |

---

## Enterprise sales

Enterprise is **not** self-serve. Flow:

1. Prospect submits “Contact sales” on `/pricing`.
2. Sales sends order form (annual minimum, custom seats/connectors/tokens).
3. Platform Super Admin sets `Plan = Enterprise` manually or via Stripe custom price.
4. Optional: dedicated VPC / on-prem per [ops-runbook.md](ops-runbook.md) Docker path.

---

## Roadmap

| Phase | Scope |
|-------|--------|
| **Q3 (current)** | Pricing page, plan catalog, Stripe Checkout scaffold, webhooks, usage read API |
| **Q3+** | Limit enforcement, Stripe Customer Portal link, `DESIGNPARTNER20` coupon |
| **Q4** | Invoice PDF, usage-based AI overage, SOC2 billing controls |
