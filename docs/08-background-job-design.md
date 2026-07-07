# StackPilot — Background Job Design

## 1. Job Infrastructure

**Engine:** Hangfire with PostgreSQL storage (MVP), evolving to dedicated message queue.

```csharp
public interface IBackgroundJobService
{
    string Enqueue<TJob>(Expression<Action<TJob>> methodCall);
    string Schedule<TJob>(Expression<Action<TJob>> methodCall, TimeSpan delay);
    void AddRecurring<TJob>(string jobId, Expression<Action<TJob>> methodCall, string cron);
}
```

## 2. Job Categories

### Connector Jobs

| Job | Trigger | Description |
|-----|---------|-------------|
| `ConnectorSyncJob` | Manual / Cron (every 6h) | Full connector sync |
| `ConnectorHealthCheckJob` | Cron (every 15m) | Health check all connectors |
| `ConnectorWebhookProcessorJob` | Webhook event | Process incoming webhook |

### Intelligence Jobs

| Job | Trigger | Description |
|-----|---------|-------------|
| `RepositoryScanJob` | After connector sync | Deep repo analysis |
| `DatabaseScanJob` | After connector sync | Schema discovery |
| `CodeIndexingJob` | After repo scan | Index code for RAG |
| `DependencyAnalysisJob` | After repo scan | Analyze dependencies |

### AI Jobs

| Job | Trigger | Description |
|-----|---------|-------------|
| `GenerateRequirementsJob` | Ticket created | AI requirements generation |
| `GenerateImplementationPlanJob` | Approval granted | AI plan generation |
| `GenerateCodeJob` | Plan approved | AI code generation |
| `GenerateDocumentationJob` | Scan completed / Manual | AI doc generation |
| `GenerateRecommendationsJob` | Cron (daily) | Batch recommendation analysis |
| `GenerateTestsJob` | Code generated | Unit test generation |

### Workflow Jobs

| Job | Trigger | Description |
|-----|---------|-------------|
| `ProcessApprovalJob` | Approval submitted | Route to next gate |
| `NotifyApprovalRequestJob` | Gate reached | Notify approvers |
| `TrackBuildJob` | PR created / Webhook | Poll GitHub Actions |
| `ProcessDeploymentJob` | Release scheduled | Execute deployment tracking |
| `PostDeploymentVerificationJob` | Deploy completed | Verification checks |

### Maintenance Jobs

| Job | Trigger | Description |
|-----|---------|-------------|
| `AuditLogCleanupJob` | Cron (monthly) | Archive old audit logs |
| `SyncHistoryCleanupJob` | Cron (weekly) | Prune old sync history |
| `TokenRefreshJob` | Cron (hourly) | Refresh expiring OAuth tokens |

## 3. Job Configuration

```json
{
  "Hangfire": {
    "WorkerCount": 4,
    "Queues": ["critical", "default", "low"],
    "RetryAttempts": 3,
    "RetryDelays": [60, 300, 900]
  }
}
```

Queue priority:
- `critical` — Approval notifications, webhook processing
- `default` — Scans, AI generation, indexing
- `low` — Recommendations, cleanup, maintenance

## 4. Job Monitoring

- Hangfire dashboard (admin-only)
- OpenTelemetry spans for each job execution
- Job failure alerts via notification service
- Dead letter queue for permanently failed jobs

## 5. Idempotency

All jobs must be idempotent:
- Use `job_id` + `entity_id` as idempotency key
- Check for existing results before processing
- Safe to retry on failure

## 6. Rate Limiting

| Resource | Limit |
|----------|-------|
| AI API calls | 100/hour per org (configurable) |
| GitHub API | Respect rate limit headers |
| DB connections | Pool per connector instance |
| Concurrent scans | 2 per workspace |
