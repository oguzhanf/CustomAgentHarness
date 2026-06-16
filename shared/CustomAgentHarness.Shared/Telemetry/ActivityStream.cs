namespace YourCustomAgentHarness.Shared.Telemetry;

using System.Collections.Concurrent;
using System.Text.Json.Serialization;

/// <summary>
/// In-memory ring buffer for the harness Activity Console. Custom OTel-style
/// span/event sink that the harness.api streams to the web UI via Server-Sent Events.
/// Decoupled from the Microsoft.OpenTelemetry exporter so events from any of our
/// processes (agent, api, kb-mcp) show consistently in the UI.
/// </summary>
public sealed class ActivityStream
{
    private static readonly Lazy<ActivityStream> _instance = new(() => new ActivityStream());
    public static ActivityStream Instance => _instance.Value;

    private readonly ConcurrentQueue<ActivityEvent> _buffer = new();
    private readonly int _capacity = 5000;
    public event Action<ActivityEvent>? OnEvent;

    public void Emit(ActivityEvent ev)
    {
        _buffer.Enqueue(ev);
        while (_buffer.Count > _capacity && _buffer.TryDequeue(out _)) { }
        OnEvent?.Invoke(ev);
    }

    public IReadOnlyList<ActivityEvent> Snapshot() => _buffer.ToArray();
}

public sealed record ActivityEvent(
    [property: JsonPropertyName("ts")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("durationMs")] double? DurationMs,
    [property: JsonPropertyName("attributes")] Dictionary<string, object?>? Attributes)
{
    public static ActivityEvent Create(string source, string kind, string name, string status = "ok", Dictionary<string, object?>? attrs = null, double? durationMs = null)
        => new(DateTimeOffset.UtcNow, source, kind, name, status, durationMs, attrs);
}
