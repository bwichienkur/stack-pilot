namespace StackPilot.Application.AI;

public class AiCompletionRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public string? Model { get; set; }
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
    public List<AiToolDefinition>? Tools { get; set; }
}

public class AiCompletionResult
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public List<AiToolCall>? ToolCalls { get; set; }
}

public class AiEmbeddingRequest
{
    public List<string> Texts { get; set; } = [];
    public string? Model { get; set; }
}

public class AiEmbeddingResult
{
    public List<float[]> Embeddings { get; set; } = [];
    public int TokensUsed { get; set; }
}

public class AiToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersSchema { get; set; } = "{}";
}

public class AiToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
}

public interface IAiProvider
{
    string ProviderName { get; }
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default);
    Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken ct = default);
}

public interface IAiGovernanceService
{
    Task<Guid> RecordActionAsync(string actionType, string? input, string? output, string model, int tokens, bool isReversible, CancellationToken ct = default);
    Task<bool> RequiresApprovalAsync(string actionType);
    Task EnsureTicketApprovalAsync(Guid ticketId, string actionType, CancellationToken ct = default);
    Task<AiActionReversalResult> ReverseActionAsync(Guid actionId, CancellationToken ct = default);
}

public record AiActionReversalResult(Guid OriginalActionId, Guid ReversalActionId, string Message);
