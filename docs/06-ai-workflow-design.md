# StackPilot — AI Workflow Design

## 1. AI Provider Abstraction

```csharp
public interface IAiProvider
{
    string ProviderName { get; }
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct);
    Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct);
    Task<AiToolResult> ExecuteToolsAsync(AiToolRequest request, CancellationToken ct);
}
```

Implementations: `OpenAiProvider`, `AnthropicProvider`, `AzureOpenAiProvider`, `MockAiProvider` (dev/test).

## 2. RAG Pipeline

```
User Query / Ticket Description
        ↓
Query Embedding (IAiProvider.EmbedAsync)
        ↓
Vector Search (pgvector / Redis)
  ├── Code chunks (indexed from repo scans)
  ├── Documentation chunks
  ├── Database metadata
  ├── Graph node summaries
  └── Ticket history
        ↓
Context Assembly (top-K relevant chunks)
        ↓
LLM Completion with system prompt + context + tools
        ↓
Response + Citations + Audit Log
```

### Index Sources
| Source | Chunk Strategy |
|--------|---------------|
| Repository code | File-level + function-level chunks |
| Documentation | Section-level markdown chunks |
| Database schema | Table + column descriptions |
| Graph nodes | Node metadata summaries |
| Tickets | Description + requirements + comments |

## 3. AI Action Types

| Action | Approval Required | Reversible |
|--------|-------------------|------------|
| Generate requirements | No | Yes (regenerate) |
| Generate implementation plan | Yes (Architect) | Yes |
| Generate code changes | Yes (Technical + Security) | Yes (revert PR) |
| Generate DB migration | Yes (DB Admin) | Yes |
| Generate documentation | No | Yes (regenerate) |
| Create pull request | Yes (Technical) | Yes (close PR) |
| Trigger deployment | Yes (Release Manager) | Yes (rollback) |
| Execute code in sandbox | Yes (Technical) | N/A |

## 4. Governance Model

Every AI action records:

```json
{
  "actionId": "uuid",
  "actionType": "generate_code",
  "what": "Modify UserService.cs to add email validation",
  "why": "Ticket #1234: Add email validation per requirements",
  "affectedFiles": ["src/Services/UserService.cs"],
  "affectedRepos": ["main-api"],
  "affectedDatabases": [],
  "riskScore": 3.5,
  "confidenceScore": 0.87,
  "testsToRun": ["UserServiceTests"],
  "rollbackPlan": "Revert commit abc123",
  "approvals": [
    { "type": "technical", "approverId": "...", "decision": "approved" }
  ],
  "model": "gpt-4o",
  "tokensUsed": 4500,
  "timestamp": "2026-07-07T10:00:00Z"
}
```

## 5. Tool-Based AI Actions

Available tools for the AI agent:

| Tool | Description | Requires Approval |
|------|-------------|-------------------|
| `search_graph` | Query knowledge graph | No |
| `search_code` | Semantic code search | No |
| `read_file` | Read file from connected repo | No |
| `generate_requirements` | Draft ticket requirements | No |
| `generate_plan` | Create implementation plan | Yes |
| `create_branch` | Create git branch | Yes |
| `write_file` | Write/modify file | Yes |
| `create_migration` | Generate DB migration | Yes |
| `create_pr` | Open pull request | Yes |
| `trigger_build` | Start CI build | Yes |
| `generate_tests` | Create unit tests | Yes |

## 6. Ticket AI Workflow

```
1. CREATE TICKET
   User submits ticket with title, description, business justification
        ↓
2. AI ANALYSIS (automatic)
   - RAG over graph + code + docs
   - Generate: business summary, functional reqs, non-functional reqs,
     acceptance criteria, impact analysis, risk score, confidence score
   - Status → Requirements Drafted
        ↓
3. APPROVAL GATE
   - Architect reviews requirements
   - Security reviews if security-related
   - DB Admin reviews if DB change
        ↓
4. IMPLEMENTATION PLAN (on approval)
   - AI generates technical plan: files, APIs, migrations, test plan
   - Status → Approved
        ↓
5. CODE GENERATION (on plan approval)
   - AI creates branch, generates changes, runs static validation
   - Opens PR, links to ticket
   - Status → Pull Request Created
        ↓
6. CI/CD
   - GitHub Actions build/test
   - Results attached to ticket
   - Status → QA In Progress (on pass)
        ↓
7. QA → UAT → Production (human gates at each step)
```

## 7. Safety Constraints

- AI NEVER writes directly to production
- AI NEVER deploys without explicit human approval
- All generated code goes through PR review workflow
- Database migrations require DB admin approval
- Rollback plan required for every change action
- Rate limiting on AI calls per organization
- Token budget limits per plan tier

## 8. Prompt Templates

Stored as versioned templates in `ai_prompt_templates` table:
- `requirements_generation_v1`
- `implementation_plan_v1`
- `code_generation_v1`
- `documentation_generation_v1`
- `recommendation_analysis_v1`
- `assistant_system_v1`

Each template includes: system prompt, user prompt template, output schema, required context sources.
