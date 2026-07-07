# StackPilot — API Design

Base URL: `https://api.stackpilot.io/v1`

Authentication: Bearer JWT token. All endpoints require `Authorization: Bearer <token>` unless noted.

Headers:
- `X-Organization-Id` — Required for tenant-scoped operations
- `X-Workspace-Id` — Optional workspace scope

## Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/auth/register` | Register new user |
| POST | `/auth/login` | Login, returns JWT |
| POST | `/auth/refresh` | Refresh token |
| GET | `/auth/me` | Current user profile |

## Organizations & Workspaces

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/organizations` | List user's organizations |
| POST | `/organizations` | Create organization |
| GET | `/organizations/{id}` | Get organization |
| PATCH | `/organizations/{id}` | Update organization |
| GET | `/organizations/{id}/workspaces` | List workspaces |
| POST | `/organizations/{id}/workspaces` | Create workspace |
| GET | `/workspaces/{id}` | Get workspace |
| GET | `/organizations/{id}/members` | List members |
| POST | `/organizations/{id}/members` | Invite member |
| GET | `/organizations/{id}/teams` | List teams |
| POST | `/organizations/{id}/teams` | Create team |

## Connectors

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/connectors/definitions` | List available connector types |
| GET | `/workspaces/{id}/connectors` | List workspace connectors |
| POST | `/workspaces/{id}/connectors` | Create connector instance |
| GET | `/connectors/{id}` | Get connector details |
| PATCH | `/connectors/{id}` | Update connector config |
| DELETE | `/connectors/{id}` | Remove connector |
| POST | `/connectors/{id}/test` | Test connection |
| POST | `/connectors/{id}/sync` | Trigger sync |
| GET | `/connectors/{id}/sync-history` | Sync history |
| GET | `/connectors/{id}/health` | Health status |

## Intelligence

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workspaces/{id}/repositories` | List scanned repositories |
| GET | `/repositories/{id}` | Repository scan details |
| POST | `/repositories/{id}/scan` | Trigger repo scan |
| GET | `/workspaces/{id}/databases` | List scanned databases |
| GET | `/databases/{id}` | Database scan details |
| POST | `/databases/{id}/scan` | Trigger DB scan |

## Knowledge Graph

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workspaces/{id}/graph/nodes` | List/search nodes |
| GET | `/graph/nodes/{id}` | Node details with edges |
| GET | `/workspaces/{id}/graph/edges` | List edges |
| POST | `/workspaces/{id}/graph/search` | Full-text graph search |
| POST | `/graph/nodes/{id}/impact` | Impact analysis |

## Documentation

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workspaces/{id}/docs` | List documentation pages |
| GET | `/docs/{id}` | Get doc with latest version |
| POST | `/docs/{id}/generate` | AI generate documentation |
| GET | `/docs/{id}/versions` | Version history |
| POST | `/docs/{id}/versions` | Create manual version |

## Recommendations

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workspaces/{id}/recommendations` | List recommendations |
| GET | `/recommendations/{id}` | Recommendation details |
| PATCH | `/recommendations/{id}` | Update status (dismiss, accept) |

## Tickets & Workflow

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/workspaces/{id}/tickets` | List tickets (filterable) |
| POST | `/workspaces/{id}/tickets` | Create ticket |
| GET | `/tickets/{id}` | Ticket details |
| PATCH | `/tickets/{id}` | Update ticket |
| POST | `/tickets/{id}/comments` | Add comment |
| POST | `/tickets/{id}/attachments` | Upload attachment |
| POST | `/tickets/{id}/generate-requirements` | AI generate requirements |
| POST | `/tickets/{id}/generate-plan` | AI generate implementation plan |
| GET | `/tickets/{id}/approvals` | List approvals |
| POST | `/tickets/{id}/approvals` | Submit approval decision |
| POST | `/tickets/{id}/qa` | Submit QA evidence |
| POST | `/tickets/{id}/uat` | Submit UAT decision |
| POST | `/tickets/{id}/schedule-release` | Schedule production release |

## CI/CD

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/tickets/{id}/builds` | Build runs for ticket |
| GET | `/tickets/{id}/deployments` | Deployment history |
| POST | `/tickets/{id}/create-pr` | AI create pull request |

## AI Assistant

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/workspaces/{id}/ai/chat` | Chat with AI assistant |
| GET | `/workspaces/{id}/ai/conversations` | List conversations |
| GET | `/ai/conversations/{id}` | Conversation history |

## Audit & Settings

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/organizations/{id}/audit-logs` | Audit log (paginated) |
| GET | `/organizations/{id}/settings` | Client settings |
| PATCH | `/organizations/{id}/settings` | Update settings |

## Response Format

```json
{
  "data": { },
  "meta": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 150
  },
  "errors": null
}
```

## Error Format

```json
{
  "data": null,
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "message": "Title is required",
      "field": "title"
    }
  ]
}
```

## WebSocket Events

`wss://api.stackpilot.io/v1/ws`

Events: `scan.completed`, `ticket.updated`, `approval.requested`, `build.completed`, `deployment.started`
