namespace YourCustomAgentHarness.KbMcp;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using YourCustomAgentHarness.Shared.Telemetry;

/// <summary>
/// In-memory embedding index over markdown KB files.
/// Calls Azure OpenAI embeddings (text-embedding-3-small) via raw HTTP. Prefers Entra-auth
/// (DefaultAzureCredential → cognitiveservices.azure.com/.default) so it works against
/// Foundry accounts that have disableLocalAuth=true. Falls back to API key if one is
/// configured (set AzureOpenAI:ApiKey in appsettings.json or AZURE_OPENAI_API_KEY env var),
/// and finally to a bag-of-words classifier so the demo still works offline.
/// </summary>
public sealed class EmbeddingIndex
{
    private readonly List<Chunk> _chunks = new();
    private readonly IHttpClientFactory _http;
    private readonly ILogger<EmbeddingIndex> _log;
    private string? _aoaiEndpoint;
    private string? _aoaiKey;
    private string _embeddingDeployment = "text-embedding-3-small";
    private bool _useRealEmbeddings;
    private TokenCredential? _tokenCredential;
    private static readonly string[] AoaiScope = new[] { "https://cognitiveservices.azure.com/.default" };

    public int DocumentCount { get; private set; }
    public int ChunkCount => _chunks.Count;
    public DateTimeOffset IndexedAt { get; private set; }

    public EmbeddingIndex(IHttpClientFactory http, ILogger<EmbeddingIndex> log)
    {
        _http = http;
        _log = log;
    }

    public async Task LoadAsync(string kbRoot, IConfiguration cfg)
    {
        _aoaiEndpoint = cfg["AzureOpenAI:Endpoint"] ?? Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT") ?? "";
        _aoaiKey      = cfg["AzureOpenAI:ApiKey"]     ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        _embeddingDeployment = cfg["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-small";

        if (!string.IsNullOrWhiteSpace(_aoaiKey))
        {
            _useRealEmbeddings = true;
        }
        else
        {
            // No key — try Entra-auth. We'll verify by acquiring a token now (fast-fail);
            // if it works we use real embeddings, otherwise fall back to bag-of-words.
            try
            {
                _tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeInteractiveBrowserCredential = false,
                });
                _ = await _tokenCredential.GetTokenAsync(new TokenRequestContext(AoaiScope), CancellationToken.None);
                _useRealEmbeddings = true;
                _log.LogInformation("KB embeddings: using DefaultAzureCredential (Entra-auth) against {endpoint}", _aoaiEndpoint);
            }
            catch (Exception ex)
            {
                _useRealEmbeddings = false;
                _log.LogWarning(ex, "KB embeddings: Entra-auth failed; falling back to bag-of-words.");
            }
        }

        if (!Directory.Exists(kbRoot))
        {
            _log.LogWarning("KB directory not found: {kb}", kbRoot);
            return;
        }

        var files = Directory.GetFiles(kbRoot, "*.md").OrderBy(f => f).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var path in files)
        {
            var text = await File.ReadAllTextAsync(path);
            var title = ExtractTitle(text) ?? Path.GetFileNameWithoutExtension(path);
            var chunks = Chunkify(text, chunkSize: 1200, overlap: 200);
            foreach (var c in chunks)
            {
                var emb = _useRealEmbeddings ? await EmbedAsync(c) : BagOfWords(c);
                _chunks.Add(new Chunk(Path.GetFileNameWithoutExtension(path), title, path, c, emb));
            }
            DocumentCount++;
        }
        sw.Stop();
        IndexedAt = DateTimeOffset.UtcNow;
        _log.LogInformation("KB indexed: {docs} docs, {chunks} chunks in {ms}ms ({mode})",
            DocumentCount, _chunks.Count, sw.ElapsedMilliseconds,
            _useRealEmbeddings ? "AOAI embeddings" : "fallback bag-of-words");
        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "customagentharness-kb-mcp", "lifecycle", "kb.indexed", "ok",
            new Dictionary<string, object?>
            {
                ["docs"] = DocumentCount,
                ["chunks"] = _chunks.Count,
                ["mode"] = _useRealEmbeddings ? "aoai" : "bow"
            }, sw.Elapsed.TotalMilliseconds));
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || _chunks.Count == 0) return Array.Empty<SearchHit>();
        var qEmb = _useRealEmbeddings ? await EmbedAsync(query) : BagOfWords(query);

        return _chunks
            .Select(c => new { c, score = CosineSimilarity(qEmb, c.Embedding) })
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => new SearchHit(x.c.DocumentId, x.c.Title, x.c.Source, x.score, Excerpt(x.c.Text)))
            .ToList();
    }

    private static string Excerpt(string s, int max = 600) => s.Length <= max ? s : s.Substring(0, max) + "…";

    private static string? ExtractTitle(string md)
    {
        foreach (var line in md.Split('\n', 10))
            if (line.StartsWith("# ")) return line.Substring(2).Trim();
        return null;
    }

    private static IEnumerable<string> Chunkify(string text, int chunkSize, int overlap)
    {
        for (int i = 0; i < text.Length; i += (chunkSize - overlap))
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
            if (i + chunkSize >= text.Length) yield break;
        }
    }

    private async Task<float[]> EmbedAsync(string text)
    {
        var http = _http.CreateClient();
        if (!string.IsNullOrWhiteSpace(_aoaiKey))
        {
            http.DefaultRequestHeaders.Add("api-key", _aoaiKey);
        }
        else if (_tokenCredential != null)
        {
            var tok = await _tokenCredential.GetTokenAsync(new TokenRequestContext(AoaiScope), CancellationToken.None);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tok.Token);
        }
        var url = $"{_aoaiEndpoint!.TrimEnd('/')}/openai/deployments/{_embeddingDeployment}/embeddings?api-version=2024-10-21";
        var body = JsonSerializer.Serialize(new { input = text });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await http.PostAsync(url, content);
        if (!resp.IsSuccessStatusCode) return BagOfWords(text);
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var emb = doc.RootElement.GetProperty("data")[0].GetProperty("embedding").EnumerateArray()
            .Select(e => e.GetSingle()).ToArray();
        return emb;
    }

    private static float[] BagOfWords(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        const int dim = 256;
        var v = new float[dim];
        foreach (var w in words)
        {
            v[Math.Abs(w.GetHashCode()) % dim] += 1f;
        }
        var norm = (float)Math.Sqrt(v.Sum(x => x * x));
        if (norm > 0) for (int i = 0; i < dim; i++) v[i] /= norm;
        return v;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private sealed record Chunk(string DocumentId, string Title, string Source, string Text, float[] Embedding);
}
