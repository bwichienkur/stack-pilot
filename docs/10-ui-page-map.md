# StackPilot — UI Page Map

## Navigation Structure

```
StackPilot
├── Dashboard (workspace overview)
├── Intelligence
│   ├── Applications
│   ├── Repositories
│   ├── Databases
│   └── Connectors
├── Architecture
│   ├── Dependency Map
│   ├── API Flow
│   ├── ERD View
│   └── Knowledge Graph Explorer
├── Documentation Hub
├── Recommendations
├── Workflow
│   ├── Ticket Board (Kanban)
│   ├── Ticket Detail
│   ├── Approval Queue
│   ├── QA Queue
│   ├── UAT Queue
│   └── Release Calendar
├── Deployments
├── AI Assistant (copilot panel)
├── Audit Logs
└── Settings
    ├── Organization
    ├── Workspaces
    ├── Teams & Users
    ├── Roles & Permissions
    ├── Connectors
    ├── Environments
    ├── Billing
    └── Integrations (SSO)
```

## Page Specifications

### Login (`/login`)
- Email/password form
- SSO buttons (disabled with "Coming Soon" in MVP)
- Dark mode branded layout

### Organization Setup (`/onboarding`)
- Step wizard: org name → workspace → invite team → connect first repo

### Workspace Dashboard (`/`)
- KPI cards: apps, repos, open tickets, risk score, recommendations
- Recent activity feed
- Architecture health chart
- Quick actions: new ticket, connect repo, ask AI

### Application Inventory (`/applications`)
- Filterable table with stack, risk, test coverage, last scan
- Click → application detail with tabs

### Repository Connections (`/connectors/repositories`)
- Connector cards with health status
- Add connector wizard
- Sync history timeline

### Database Connections (`/connectors/databases`)
- Database connector list
- Schema summary cards
- Last scan status

### Architecture Map (`/architecture`)
- Full-screen React Flow canvas
- Toolbar: filters, layout, zoom, export
- Side panel: node details on click
- Views: dependency, API flow, deployment

### Knowledge Graph Explorer (`/graph`)
- Search bar with autocomplete
- Graph visualization with expand/collapse
- Node detail panel with connected entities

### Documentation Hub (`/docs`)
- Sidebar tree navigation
- Markdown renderer with version selector
- Regenerate button, review status badge

### AI Recommendations (`/recommendations`)
- Filterable cards by type, risk, status
- Detail drawer with implementation plan

### Ticket Board (`/tickets`)
- Kanban columns by status
- Drag-and-drop (post-MVP), click to open
- Filters: type, priority, assignee

### Ticket Detail (`/tickets/[id]`)
- Header: title, status badge, priority, assignee
- Tabs: Details, Requirements, Plan, Approvals, Builds, QA, UAT, Activity
- AI copilot sidebar for context-aware assistance
- Approval flow visualization (stepper)

### Approval Queue (`/approvals`)
- Pending approvals table
- Quick approve/reject with comments

### QA Queue (`/qa`)
- Tickets awaiting QA
- Test case checklist, evidence upload

### UAT Queue (`/uat`)
- Tickets awaiting UAT
- Summary view with QA evidence

### Deployment Dashboard (`/deployments`)
- Active deployments, history timeline
- Environment status cards

### Release Calendar (`/releases`)
- Calendar view of scheduled releases
- Release detail with checklist

### Audit Logs (`/audit`)
- Searchable, filterable log table
- Export capability

### Settings (`/settings/*`)
- Tabbed settings pages per section

### Connector Marketplace (`/marketplace`)
- Grid of available connectors
- Install/configure flow

## Global UI Components

| Component | Usage |
|-----------|-------|
| Sidebar | Primary navigation |
| Command Palette (⌘K) | Quick navigation and actions |
| AI Copilot Panel | Contextual AI assistant |
| Status Badge | Ticket/connector/build status |
| Risk Indicator | Color-coded risk levels |
| Timeline | Activity, approval, deployment history |
| Data Table | Sortable, filterable enterprise tables |
| Empty States | Guided onboarding prompts |
| Toast Notifications | Action feedback |
| Modal/Drawer | Detail views, forms |

## Design Tokens

- Background: `#09090b` (zinc-950)
- Surface: `#18181b` (zinc-900)
- Border: `#27272a` (zinc-800)
- Primary: `#6366f1` (indigo-500)
- Success: `#22c55e`, Warning: `#eab308`, Danger: `#ef4444`
- Font: Inter (UI), JetBrains Mono (code)
- Radius: 8px default, 12px cards

## Responsive Breakpoints

- Desktop: 1280px+ (primary target)
- Tablet: 768px–1279px (sidebar collapses)
- Mobile: < 768px (bottom nav, simplified views)
