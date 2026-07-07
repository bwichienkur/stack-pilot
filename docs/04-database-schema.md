# StackPilot — Database Schema

## Schema Overview

PostgreSQL database `stackpilot` with shared-schema multi-tenancy. All tenant tables include `organization_id` (UUID, NOT NULL, indexed).

## Core Tables

### Tenancy

```sql
-- Organizations (tenants)
CREATE TABLE organizations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(200) NOT NULL,
    slug            VARCHAR(100) NOT NULL UNIQUE,
    plan            VARCHAR(50) NOT NULL DEFAULT 'trial',
    is_active       BOOLEAN NOT NULL DEFAULT true,
    settings_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Workspaces
CREATE TABLE workspaces (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name            VARCHAR(200) NOT NULL,
    slug            VARCHAR(100) NOT NULL,
    description     TEXT,
    is_active       BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(organization_id, slug)
);

-- Users (platform-wide identity)
CREATE TABLE users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email           VARCHAR(320) NOT NULL UNIQUE,
    password_hash   VARCHAR(500),
    first_name      VARCHAR(100),
    last_name       VARCHAR(100),
    avatar_url      VARCHAR(500),
    is_active       BOOLEAN NOT NULL DEFAULT true,
    last_login_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Organization memberships
CREATE TABLE organization_members (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    user_id         UUID NOT NULL REFERENCES users(id),
    role_id         UUID NOT NULL REFERENCES roles(id),
    joined_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(organization_id, user_id)
);

-- Teams
CREATE TABLE teams (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    workspace_id    UUID REFERENCES workspaces(id),
    name            VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Roles & Permissions
CREATE TABLE roles (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID REFERENCES organizations(id), -- NULL = system role
    name            VARCHAR(100) NOT NULL,
    description     TEXT,
    is_system       BOOLEAN NOT NULL DEFAULT false
);

CREATE TABLE permissions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code            VARCHAR(100) NOT NULL UNIQUE,
    description     TEXT
);

CREATE TABLE role_permissions (
    role_id         UUID NOT NULL REFERENCES roles(id),
    permission_id   UUID NOT NULL REFERENCES permissions(id),
    PRIMARY KEY (role_id, permission_id)
);

-- Audit logs (append-only)
CREATE TABLE audit_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    user_id         UUID,
    action          VARCHAR(100) NOT NULL,
    entity_type     VARCHAR(100),
    entity_id       UUID,
    details_json    JSONB,
    ip_address      INET,
    user_agent      TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_audit_logs_org_created ON audit_logs(organization_id, created_at DESC);
```

### Connectors

```sql
CREATE TABLE connector_definitions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type            VARCHAR(50) NOT NULL UNIQUE,
    name            VARCHAR(200) NOT NULL,
    description     TEXT,
    config_schema   JSONB NOT NULL,
    capabilities    TEXT[] NOT NULL
);

CREATE TABLE connector_instances (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    workspace_id    UUID NOT NULL REFERENCES workspaces(id),
    definition_id   UUID NOT NULL REFERENCES connector_definitions(id),
    name            VARCHAR(200) NOT NULL,
    config_json     JSONB NOT NULL,
    status          VARCHAR(50) NOT NULL DEFAULT 'pending',
    last_sync_at    TIMESTAMPTZ,
    last_health_at  TIMESTAMPTZ,
    health_status   VARCHAR(50),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE connector_credentials (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connector_id    UUID NOT NULL REFERENCES connector_instances(id),
    credential_type VARCHAR(50) NOT NULL,
    encrypted_value BYTEA NOT NULL,
    key_version     INT NOT NULL DEFAULT 1,
    expires_at      TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE sync_histories (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connector_id    UUID NOT NULL REFERENCES connector_instances(id),
    status          VARCHAR(50) NOT NULL,
    started_at      TIMESTAMPTZ NOT NULL,
    completed_at    TIMESTAMPTZ,
    items_processed INT DEFAULT 0,
    errors_json     JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

### Knowledge Graph

```sql
CREATE TABLE graph_nodes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    workspace_id    UUID,
    node_type       VARCHAR(50) NOT NULL,
    name            VARCHAR(500) NOT NULL,
    external_id     VARCHAR(500),
    metadata_json   JSONB,
    risk_score      DECIMAL(5,2),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_graph_nodes_org_type ON graph_nodes(organization_id, node_type);
CREATE INDEX idx_graph_nodes_name ON graph_nodes USING gin(to_tsvector('english', name));

CREATE TABLE graph_edges (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    source_node_id  UUID NOT NULL REFERENCES graph_nodes(id),
    target_node_id  UUID NOT NULL REFERENCES graph_nodes(id),
    edge_type       VARCHAR(50) NOT NULL,
    metadata_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(source_node_id, target_node_id, edge_type)
);
CREATE INDEX idx_graph_edges_source ON graph_edges(source_node_id);
CREATE INDEX idx_graph_edges_target ON graph_edges(target_node_id);
```

### Intelligence

```sql
CREATE TABLE repository_scans (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    connector_id    UUID NOT NULL REFERENCES connector_instances(id),
    repository_name VARCHAR(500) NOT NULL,
    status          VARCHAR(50) NOT NULL,
    results_json    JSONB,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE database_scans (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    connector_id    UUID NOT NULL REFERENCES connector_instances(id),
    database_name   VARCHAR(200) NOT NULL,
    status          VARCHAR(50) NOT NULL,
    results_json    JSONB,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

### Documentation

```sql
CREATE TABLE documentation_pages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    workspace_id    UUID,
    title           VARCHAR(500) NOT NULL,
    doc_type        VARCHAR(50) NOT NULL,
    graph_node_id   UUID REFERENCES graph_nodes(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE documentation_versions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    page_id         UUID NOT NULL REFERENCES documentation_pages(id),
    version         INT NOT NULL,
    content_md      TEXT NOT NULL,
    generated_by    VARCHAR(50) NOT NULL, -- 'ai' or 'human'
    status          VARCHAR(50) NOT NULL DEFAULT 'draft',
    created_by      UUID REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(page_id, version)
);
```

### Workflow (Tickets)

```sql
CREATE TABLE tickets (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    workspace_id    UUID NOT NULL,
    ticket_number   SERIAL,
    title           VARCHAR(500) NOT NULL,
    description     TEXT,
    ticket_type     VARCHAR(50) NOT NULL,
    status          VARCHAR(50) NOT NULL DEFAULT 'submitted',
    priority        VARCHAR(20) NOT NULL DEFAULT 'medium',
    requester_id    UUID NOT NULL REFERENCES users(id),
    assignee_id     UUID REFERENCES users(id),
    business_justification TEXT,
    ai_requirements_json JSONB,
    implementation_plan_json JSONB,
    risk_score      DECIMAL(5,2),
    confidence_score DECIMAL(5,2),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE ticket_comments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    user_id         UUID NOT NULL REFERENCES users(id),
    content         TEXT NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE ticket_attachments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    file_name       VARCHAR(500) NOT NULL,
    file_url        VARCHAR(1000) NOT NULL,
    content_type    VARCHAR(100),
    uploaded_by     UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE approvals (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    approval_type   VARCHAR(50) NOT NULL,
    approver_id     UUID NOT NULL REFERENCES users(id),
    decision        VARCHAR(20) NOT NULL, -- approved, rejected
    comments        TEXT,
    plan_version    INT,
    risk_score      DECIMAL(5,2),
    decided_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE qa_evidences (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    tester_id       UUID NOT NULL REFERENCES users(id),
    result          VARCHAR(20) NOT NULL,
    notes           TEXT,
    evidence_urls   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE uat_decisions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    approver_id     UUID NOT NULL REFERENCES users(id),
    decision        VARCHAR(20) NOT NULL,
    comments        TEXT,
    decided_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE release_schedules (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    ticket_id       UUID NOT NULL REFERENCES tickets(id),
    scheduled_at    TIMESTAMPTZ NOT NULL,
    release_window  VARCHAR(100),
    checklist_json  JSONB,
    rollback_plan   TEXT,
    status          VARCHAR(50) NOT NULL DEFAULT 'scheduled',
    deployed_at     TIMESTAMPTZ,
    verified_at     TIMESTAMPTZ,
    created_by      UUID NOT NULL REFERENCES users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

### AI & Recommendations

```sql
CREATE TABLE ai_actions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    user_id         UUID,
    action_type     VARCHAR(100) NOT NULL,
    input_json      JSONB,
    output_json     JSONB,
    model           VARCHAR(100),
    tokens_used     INT,
    status          VARCHAR(50) NOT NULL,
    is_reversible   BOOLEAN NOT NULL DEFAULT false,
    reversal_id     UUID,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE recommendations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    workspace_id    UUID,
    type            VARCHAR(50) NOT NULL,
    summary         TEXT NOT NULL,
    reasoning       TEXT,
    risk_level      VARCHAR(20) NOT NULL,
    confidence_score DECIMAL(5,2),
    affected_entities_json JSONB,
    implementation_plan TEXT,
    rollback_plan   TEXT,
    status          VARCHAR(50) NOT NULL DEFAULT 'open',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE build_runs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL,
    ticket_id       UUID REFERENCES tickets(id),
    connector_id    UUID REFERENCES connector_instances(id),
    external_id     VARCHAR(200),
    status          VARCHAR(50) NOT NULL,
    conclusion      VARCHAR(50),
    logs_url        VARCHAR(1000),
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

## Indexing Strategy

- All `organization_id` columns indexed
- Composite indexes on frequently queried combinations
- GIN indexes for full-text search on graph node names
- JSONB GIN indexes on `metadata_json` where queried

## Migration Strategy

- EF Core migrations in `StackPilot.Infrastructure`
- Seed data for system roles, permissions, connector definitions
- Versioned migration scripts for production deployments
