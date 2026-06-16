using System.Text;
using System.Text.Json;
using YourCustomAgentHarness.Shared.Telemetry;
using YourCustomAgentHarness.KbMcp;

// Load .env (if present) so a single file can supply the model key / endpoint.
YourCustomAgentHarness.Shared.DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<ActivityForwarder>();
builder.Services.AddSingleton<EmbeddingIndex>();

var app = builder.Build();
app.UseCors();

// Boot-time: load + embed all markdown KB docs
var kbRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "kb", "agenticbank"));
var index = app.Services.GetRequiredService<EmbeddingIndex>();
await index.LoadAsync(kbRoot, app.Configuration);

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    server = "customagentharness-kb-mcp",
    documents = index.DocumentCount,
    chunks = index.ChunkCount,
    indexed_at = index.IndexedAt
}));

// MCP-shaped tool listing (we expose a single tool: kb_search)
app.MapGet("/mcp/tools", () => Results.Ok(new[]
{
    new
    {
        name = "kb_search",
        description = "Search the AgenticBank policy knowledge base. Returns top matching policy excerpts.",
        inputSchema = new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Natural-language question" },
                topK = new { type = "integer", description = "How many results", @default = 5 }
            },
            required = new[] { "query" }
        }
    }
}));

app.MapPost("/mcp/tools/kb_search", async (SearchRequest req, EmbeddingIndex idx, CancellationToken ct) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var topK = req.TopK is null or <= 0 ? 5 : Math.Min(req.TopK.Value, 10);
    var results = await idx.SearchAsync(req.Query ?? "", topK, ct);
    sw.Stop();
    ActivityStream.Instance.Emit(ActivityEvent.Create(
        "customagentharness-kb-mcp", "tool", "kb_search", "ok",
        new Dictionary<string, object?> { ["q"] = req.Query, ["topK"] = topK, ["hits"] = results.Count },
        sw.Elapsed.TotalMilliseconds));
    return Results.Ok(new SearchResponse(results, idx.DocumentCount, idx.ChunkCount));
});

// Simple POST /search alias for non-MCP consumers (the agent uses this)
app.MapPost("/search", async (SearchRequest req, EmbeddingIndex idx, CancellationToken ct) =>
{
    var topK = req.TopK is null or <= 0 ? 5 : Math.Min(req.TopK.Value, 10);
    var results = await idx.SearchAsync(req.Query ?? "", topK, ct);
    return Results.Ok(new SearchResponse(results, idx.DocumentCount, idx.ChunkCount));
});

app.Run();

public sealed record SearchRequest(string? Query, int? TopK);
public sealed record SearchHit(string DocumentId, string Title, string Source, double Score, string Excerpt);
public sealed record SearchResponse(IReadOnlyList<SearchHit> Hits, int DocumentCount, int ChunkCount);
