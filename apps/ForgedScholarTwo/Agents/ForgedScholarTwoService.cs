namespace YourCustomAgentHarness.ForgedScholarTwo;

using System.Net.Http.Json;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using YourCustomAgentHarness.Shared.Manifest;
using YourCustomAgentHarness.Shared.Telemetry;

public sealed record ChatAnswer(string Text, IReadOnlyList<Citation> Citations);

public sealed class ForgedScholarTwoService
{
    private readonly Kernel _kernel;
    private readonly AgentBlueprint _blueprint;
    private readonly IHttpClientFactory _http;

    public ForgedScholarTwoService(Kernel kernel, AgentBlueprint blueprint, IHttpClientFactory http)
    {
        _kernel = kernel;
        _blueprint = blueprint;
        _http = http;
    }

    public async Task<ChatAnswer> ChatAsync(ChatRequest req, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var query = req.Message ?? "";

        // Step 1: retrieve from KB MCP
        IReadOnlyList<KbHit> hits = Array.Empty<KbHit>();
        try
        {
            var http = _http.CreateClient("kb");
            var resp = await http.PostAsJsonAsync("/search", new { query, topK = 5 }, ct);
            if (resp.IsSuccessStatusCode)
            {
                var payload = await resp.Content.ReadFromJsonAsync<SearchPayload>(cancellationToken: ct);
                hits = payload?.hits ?? Array.Empty<KbHit>();
            }
        }
        catch (Exception ex)
        {
            ActivityStream.Instance.Emit(ActivityEvent.Create(
                "ForgedScholarTwo", "mcp", "kb.search", "error",
                new Dictionary<string, object?> { ["error"] = ex.Message }));
        }

        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "ForgedScholarTwo", "mcp", "kb.search", "ok",
            new Dictionary<string, object?> { ["query"] = query, ["hits"] = hits.Count }));

        // Step 2: build grounded prompt
        var system = new StringBuilder();
        system.AppendLine("You are ForgedScholarTwo, AgenticBank's policy & compliance knowledge agent.");
        system.AppendLine($"You are running inside the {nameof(YourCustomAgentHarness)} custom agent harness with its OWN agent identity (application permissions).");
        system.AppendLine("Answer ONLY from the provided KB excerpts. If the excerpts do not contain the answer, say so plainly.");
        system.AppendLine("ALWAYS cite the document id (e.g. [01-kyc-policy]) inline next to claims.");
        system.AppendLine("Never invent account numbers, IBANs, card numbers or other sensitive identifiers.");
        system.AppendLine();
        system.AppendLine("### KB excerpts");
        if (hits.Count == 0) system.AppendLine("(none — KB returned no matches)");
        foreach (var h in hits)
        {
            system.AppendLine($"---");
            system.AppendLine($"[{h.documentId}] {h.title} (score={h.score:F3})");
            system.AppendLine(h.excerpt);
        }

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(system.ToString());
        history.AddUserMessage(query);

        try
        {
            var result = await chat.GetChatMessageContentAsync(history,
                new OpenAIPromptExecutionSettings
                {
                    Temperature = _blueprint.Model.Temperature,
                    MaxTokens = _blueprint.Model.MaxTokens
                }, _kernel, ct);
            sw.Stop();
            ActivityStream.Instance.Emit(ActivityEvent.Create(
                "ForgedScholarTwo", "model", "chat.complete", "ok", durationMs: sw.Elapsed.TotalMilliseconds));
            return new ChatAnswer(
                result.Content ?? "(empty)",
                hits.Select(h => new Citation(h.documentId, h.title, h.source, h.score)).ToList());
        }
        catch (Exception ex)
        {
            sw.Stop();
            ActivityStream.Instance.Emit(ActivityEvent.Create(
                "ForgedScholarTwo", "model", "chat.complete", "error",
                new Dictionary<string, object?> { ["error"] = ex.Message }, sw.Elapsed.TotalMilliseconds));
            return new ChatAnswer(
                $"I'm unable to reach the Azure OpenAI deployment right now ({ex.GetType().Name}).",
                Array.Empty<Citation>());
        }
    }

    private sealed record SearchPayload(KbHit[] hits, int documentCount, int chunkCount);
    private sealed record KbHit(string documentId, string title, string source, double score, string excerpt);
}
