namespace YourCustomAgentHarness.Tui.Runtime;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Demo-run state. Kept SEPARATE from <c>tenant-state.yaml</c> on purpose —
/// the rubber-duck critique flagged that mutating the source-of-truth from a
/// live demo is dangerous if something goes wrong mid-run.
/// </summary>
public sealed class DemoState
{
    [JsonPropertyName("currentChapter")]
    public int CurrentChapter { get; set; }

    [JsonPropertyName("currentStep")]
    public int CurrentStep { get; set; }

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();

    [JsonPropertyName("agentAppIds")]
    public Dictionary<string, string> AgentAppIds { get; set; } = new();

    [JsonPropertyName("blueprintAppIds")]
    public Dictionary<string, string> BlueprintAppIds { get; set; } = new();

    [JsonPropertyName("purviewReady")]
    public bool? PurviewReady { get; set; }

    [JsonPropertyName("purviewMode")]
    public string PurviewMode { get; set; } = "unknown";

    [JsonPropertyName("lastRunAt")]
    public DateTimeOffset LastRunAt { get; set; } = DateTimeOffset.UtcNow;

    public static DemoState Load()
    {
        if (!File.Exists(HarnessPaths.DemoStateJson)) return new DemoState();
        try
        {
            var raw = File.ReadAllText(HarnessPaths.DemoStateJson);
            return JsonSerializer.Deserialize<DemoState>(raw) ?? new DemoState();
        }
        catch
        {
            return new DemoState();
        }
    }

    public void Save()
    {
        LastRunAt = DateTimeOffset.UtcNow;
        File.WriteAllText(
            HarnessPaths.DemoStateJson,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
