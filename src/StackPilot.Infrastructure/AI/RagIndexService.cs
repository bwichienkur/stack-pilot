using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackPilot.Application.AI;
using StackPilot.Application.Interfaces;
using StackPilot.Domain.Entities;
using StackPilot.Infrastructure.Persistence;

namespace StackPilot.Infrastructure.AI;

public class RagIndexService : IRagIndexService
{
    private readonly AppDbContext _db;
    private readonly IAiProvider _provider;

    public RagIndexService(AppDbContext db, IAiProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    private bool UsePgVector =>
        _db.Database.IsRelational() && _db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    public async Task IndexRepositoryScanAsync(Guid organizationId, Guid workspaceId, Guid? graphNodeId, string repositoryName, string scanResultsJson, CancellationToken ct = default)
    {
        var chunks = BuildChunks(repositoryName, scanResultsJson);
        if (chunks.Count == 0) return;

        var embedResult = await _provider.EmbedAsync(new AiEmbeddingRequest { Texts = chunks.Select(c => c.Content).ToList() }, ct);

        for (var i = 0; i < chunks.Count; i++)
        {
            var embedding = i < embedResult.Embeddings.Count ? embedResult.Embeddings[i] : Array.Empty<float>();
            var chunk = new GraphChunk
            {
                OrganizationId = organizationId,
                WorkspaceId = workspaceId,
                GraphNodeId = graphNodeId,
                Content = chunks[i].Content,
                SourceType = chunks[i].SourceType,
                EmbeddingJson = JsonSerializer.Serialize(embedding)
            };
            _db.GraphChunks.Add(chunk);
            await _db.SaveChangesAsync(ct);

            if (UsePgVector && embedding.Length > 0)
            {
                var vectorLiteral = "[" + string.Join(",", embedding.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
                await _db.Database.ExecuteSqlRawAsync(
                    """UPDATE "GraphChunks" SET "EmbeddingVector" = {0}::vector WHERE "Id" = {1}""",
                    vectorLiteral, chunk.Id);
            }
        }
    }

    public async Task<List<RagSearchResult>> SearchAsync(Guid workspaceId, string query, int topK = 10, CancellationToken ct = default)
    {
        var queryEmbed = await _provider.EmbedAsync(new AiEmbeddingRequest { Texts = [query] }, ct);
        if (queryEmbed.Embeddings.Count == 0) return [];

        var queryVector = queryEmbed.Embeddings[0];

        if (UsePgVector && queryVector.Length > 0)
        {
            var vectorLiteral = "[" + string.Join(",", queryVector.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";
            var rows = await _db.Database.SqlQueryRaw<RagChunkRow>(
                """
                SELECT "GraphNodeId", "Content", 1 - ("EmbeddingVector" <=> {0}::vector) AS "Score"
                FROM "GraphChunks"
                WHERE "WorkspaceId" = {1} AND "EmbeddingVector" IS NOT NULL
                ORDER BY "EmbeddingVector" <=> {0}::vector
                LIMIT {2}
                """,
                vectorLiteral, workspaceId, topK).ToListAsync(ct);

            if (rows.Count > 0)
                return rows.Select(r => new RagSearchResult(r.GraphNodeId, r.Content, r.Score)).ToList();
        }

        var chunks = await _db.GraphChunks
            .Where(c => c.WorkspaceId == workspaceId)
            .Take(500)
            .ToListAsync(ct);

        return chunks
            .Select(c =>
            {
                var vector = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson) ?? [];
                return new RagSearchResult(c.GraphNodeId, c.Content, CosineSimilarity(queryVector, vector));
            })
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private sealed class RagChunkRow
    {
        public Guid? GraphNodeId { get; set; }
        public string Content { get; set; } = "";
        public double Score { get; set; }
    }

    private static List<(string Content, string SourceType)> BuildChunks(string repositoryName, string scanResultsJson)
    {
        var chunks = new List<(string, string)>
        {
            ($"Repository: {repositoryName}", "repository")
        };

        try
        {
            using var doc = JsonDocument.Parse(scanResultsJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("technologyStack", out var stack))
                chunks.Add(($"Technology stack for {repositoryName}: {stack}", "stack"));

            if (root.TryGetProperty("applicationName", out var app))
                chunks.Add(($"Application name: {app.GetString()}", "application"));

            if (root.TryGetProperty("securityRisks", out var risks) && risks.ValueKind == JsonValueKind.Array)
            {
                foreach (var risk in risks.EnumerateArray())
                    chunks.Add(($"Security risk in {repositoryName}: {risk}", "security"));
            }
        }
        catch
        {
            chunks.Add(($"Scan results for {repositoryName}: {scanResultsJson[..Math.Min(500, scanResultsJson.Length)]}", "scan"));
        }

        return chunks;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
