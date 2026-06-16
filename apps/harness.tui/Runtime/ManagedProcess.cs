namespace YourCustomAgentHarness.Tui.Runtime;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// A child process the harness manages on behalf of the demo. Carries enough
/// metadata to find/kill it again later, plus the health URL the
/// <see cref="HealthChecker"/> uses to decide if it's ready.
/// </summary>
public sealed class ManagedProcess
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("pid")]
    public int Pid { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("healthUrl")]
    public string? HealthUrl { get; init; }

    [JsonPropertyName("logPath")]
    public string? LogPath { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public Process? Live { get; set; }

    public bool IsAlive()
    {
        try
        {
            if (Live is { HasExited: false }) return true;
            var p = Process.GetProcessById(Pid);
            return !p.HasExited;
        }
        catch
        {
            return false;
        }
    }

    public void Kill()
    {
        try
        {
            if (Live is { HasExited: false })
            {
                Live.Kill(entireProcessTree: true);
                return;
            }
            var p = Process.GetProcessById(Pid);
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch
        {
            // already gone
        }
    }
}

internal sealed record ProcessStateFile(
    [property: JsonPropertyName("processes")] List<ManagedProcess> Processes);

/// <summary>
/// Reads/writes <c>state/processes.json</c> so <c>harness up</c>, <c>down</c>
/// and <c>status</c> share the same view of which child processes the harness owns.
/// </summary>
public static class ProcessStateStore
{
    public static string StatePath { get; }

    static ProcessStateStore()
    {
        var root = HarnessPaths.RepoRoot;
        var dir = Path.Combine(root, "state");
        Directory.CreateDirectory(dir);
        StatePath = Path.Combine(dir, "processes.json");
    }

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static List<ManagedProcess> Load()
    {
        if (!File.Exists(StatePath)) return new();
        try
        {
            var raw = File.ReadAllText(StatePath);
            var file = JsonSerializer.Deserialize<ProcessStateFile>(raw);
            return file?.Processes ?? new();
        }
        catch
        {
            return new();
        }
    }

    public static void Save(IEnumerable<ManagedProcess> procs)
    {
        var file = new ProcessStateFile(procs.ToList());
        File.WriteAllText(StatePath, JsonSerializer.Serialize(file, _opts));
    }

    public static void Clear()
    {
        if (File.Exists(StatePath)) File.Delete(StatePath);
    }
}
