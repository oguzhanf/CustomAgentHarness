namespace YourCustomAgentHarness.Api;

using YourCustomAgentHarness.Shared.Manifest;

public sealed record BlueprintEntry(AgentBlueprint Blueprint, string FileName, bool IsDraft);

public sealed class BlueprintRegistry
{
    private readonly string _dir;
    private List<BlueprintEntry> _cache = new();
    private readonly object _gate = new();

    public BlueprintRegistry(string dir) { _dir = dir; Reload(); }

    public IReadOnlyList<BlueprintEntry> All()
    {
        lock (_gate) return _cache.ToList();
    }

    public AgentBlueprint? Get(string id)
    {
        lock (_gate)
            return _cache
                .FirstOrDefault(e => string.Equals(e.Blueprint.Metadata.Id, id, StringComparison.OrdinalIgnoreCase))
                ?.Blueprint;
    }

    public BlueprintEntry? GetEntry(string id)
    {
        lock (_gate)
            return _cache.FirstOrDefault(e =>
                string.Equals(e.Blueprint.Metadata.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Accepts a raw YAML body (legacy) or a JSON envelope { fileName, yaml }.
    /// Returns the saved blueprint plus the canonical file name.
    /// </summary>
    public BlueprintEntry SaveDraft(string yaml, string? requestedFileName = null)
    {
        var deserializer = BlueprintLoader.CreateDeserializer();
        var bp = deserializer.Deserialize<AgentBlueprint>(yaml)
                 ?? throw new InvalidOperationException("Invalid YAML");
        if (string.IsNullOrWhiteSpace(bp.Metadata.Id))
            throw new InvalidOperationException("metadata.id required");

        Directory.CreateDirectory(_dir);

        var fileName = SanitizeFileName(requestedFileName)
                       ?? $"{bp.Metadata.Id}.draft.harness.yaml";
        if (!fileName.EndsWith(".harness.yaml", StringComparison.OrdinalIgnoreCase))
            fileName += ".harness.yaml";

        var path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, yaml);
        Reload();

        var entry = _cache.FirstOrDefault(e =>
            string.Equals(e.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        return entry ?? new BlueprintEntry(bp, fileName, IsDraftName(fileName));
    }

    private void Reload()
    {
        if (!Directory.Exists(_dir)) { lock (_gate) _cache = new(); return; }
        var files = Directory.GetFiles(_dir, "*.harness.yaml");
        var list = new List<BlueprintEntry>();
        foreach (var f in files)
        {
            try
            {
                var bp = BlueprintLoader.LoadYaml(f);
                var name = Path.GetFileName(f);
                list.Add(new BlueprintEntry(bp, name, IsDraftName(name)));
            }
            catch { /* skip malformed */ }
        }
        lock (_gate) _cache = list;
    }

    private static bool IsDraftName(string fileName) =>
        fileName.Contains(".draft.", StringComparison.OrdinalIgnoreCase);

    private static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        var bad = Path.GetInvalidFileNameChars();
        if (trimmed.IndexOfAny(bad) >= 0 || trimmed.Contains("..") || trimmed.Contains('/') || trimmed.Contains('\\'))
            return null;
        return trimmed;
    }
}
