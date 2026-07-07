# StackPilot — Module Breakdown

## Module 1: Identity & Tenancy (`StackPilot.Modules.Tenancy`)

**Responsibility:** Multi-tenant SaaS foundation, authentication, authorization, audit.

| Component | Description |
|-----------|-------------|
| `Organization` | Top-level tenant |
| `Workspace` | Sub-division within org (team/project) |
| `User` | Platform user with org membership |
| `Team` | Group of users |
| `Role` | Named permission bundle |
| `Permission` | Granular action (e.g., `tickets:approve`) |
| `AuditLog` | Immutable action record |
| `ClientSettings` | Org-specific configuration |
| `EnvironmentConfig` | Dev/Test/Prod environment definitions |
| `SecretVault` | Encrypted secrets storage |

**Key Services:** `IAuthService`, `ITenantContext`, `IAuditService`, `IRbacService`

---

## Module 2: Connectors (`StackPilot.Modules.Connectors`)

**Responsibility:** Extensible integration framework.

| Component | Description |
|-----------|-------------|
| `IConnector` | Base interface all connectors implement |
| `ConnectorDefinition` | Metadata (type, capabilities, config schema) |
| `ConnectorInstance` | Configured connector for a workspace |
| `ConnectorCredential` | Encrypted auth tokens |
| `SyncJob` | Background sync execution |
| `SyncHistory` | Sync results and errors |
| `ConnectorHealth` | Health check status |

**MVP Connectors:**
- `GitHubRepositoryConnector`
- `SqlServerConnector`
- `PostgreSQLConnector`
- `GitHubActionsConnector`

**Key Services:** `IConnectorRegistry`, `IConnectorSyncService`, `ICredentialEncryption`

---

## Module 3: Intelligence (`StackPilot.Modules.Intelligence`)

**Responsibility:** Scan and analyze connected assets.

| Component | Description |
|-----------|-------------|
| `RepositoryScan` | Scan job and results for a repo |
| `RepositoryMetadata` | Stack, structure, APIs, dependencies |
| `DatabaseScan` | Schema discovery results |
| `DatabaseMetadata` | Tables, columns, indexes, relationships |
| `ScanScheduler` | Background scan orchestration |

**Key Services:** `IRepositoryScanner`, `IDatabaseScanner`, `ICodeIndexer`

---

## Module 4: Knowledge Graph (`StackPilot.Modules.Graph`)

**Responsibility:** Central connected model of the software ecosystem.

| Component | Description |
|-----------|-------------|
| `GraphNode` | Typed node (Application, Repository, Table, etc.) |
| `GraphEdge` | Typed relationship (depends_on, calls, etc.) |
| `GraphQuery` | Search and traversal |
| `ImpactAnalysis` | Change impact computation |

**Key Services:** `IGraphService`, `IImpactAnalysisService`, `IGraphSearchService`

---

## Module 5: Documentation (`StackPilot.Modules.Documentation`)

**Responsibility:** AI-generated versioned documentation.

| Component | Description |
|-----------|-------------|
| `DocumentationPage` | Versioned doc with content |
| `DocumentationVersion` | Version history |
| `DocumentationLink` | Link to graph nodes |

**Key Services:** `IDocumentationService`, `IDocumentationGenerator`

---

## Module 6: Recommendations (`StackPilot.Modules.Recommendations`)

**Responsibility:** AI-driven continuous recommendations.

| Component | Description |
|-----------|-------------|
| `Recommendation` | Suggestion with risk, confidence, plan |
| `RecommendationType` | Refactor, security, test, dependency, etc. |

**Key Services:** `IRecommendationEngine`, `IRecommendationService`

---

## Module 7: Workflow (`StackPilot.Modules.Workflow`)

**Responsibility:** Tickets, approvals, QA, UAT, releases.

| Component | Description |
|-----------|-------------|
| `Ticket` | Change request with full lifecycle |
| `TicketComment` | Discussion thread |
| `TicketAttachment` | Files, screenshots |
| `Approval` | Approval record with decision |
| `ApprovalGate` | Required approval type configuration |
| `QaEvidence` | QA test results and artifacts |
| `UatDecision` | UAT accept/reject |
| `ReleaseSchedule` | Production deployment scheduling |
| `DeploymentRecord` | Deployment history |

**Key Services:** `ITicketService`, `IApprovalService`, `IReleaseService`

---

## Module 8: AI Engine (`StackPilot.Modules.AI`)

**Responsibility:** AI orchestration with governance.

| Component | Description |
|-----------|-------------|
| `IAiProvider` | Abstract AI provider |
| `AiAction` | Auditable AI action record |
| `AiTool` | Tool definition for agent actions |
| `RagIndex` | Vector index for RAG |
| `AiConversation` | Assistant chat history |

**Key Services:** `IAiOrchestrator`, `IRagService`, `IAiGovernanceService`

---

## Module 9: CI/CD (`StackPilot.Modules.CiCd`)

**Responsibility:** Build and deployment tracking.

| Component | Description |
|-----------|-------------|
| `BuildRun` | CI build execution |
| `TestResult` | Test outcomes |
| `Deployment` | Environment deployment record |
| `PullRequest` | Linked PR metadata |

**Key Services:** `ICiCdService`, `IDeploymentService`

---

## Cross-Cutting Concerns

| Concern | Implementation |
|---------|----------------|
| Validation | FluentValidation |
| Mapping | Mapster or manual mappers |
| Events | MediatR domain events |
| Caching | Redis via `ICacheService` |
| Notifications | `INotificationService` (email, push hooks) |
