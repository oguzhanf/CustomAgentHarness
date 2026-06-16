namespace YourCustomAgentHarness.Shared.Telemetry;

using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Listens to the local ActivityStream and forwards every event to
/// harness.api's /api/_ingest endpoint so they appear in the central
/// Activity Console of the web UI. Failures are swallowed (best effort).
/// </summary>
public sealed class ActivityForwarder : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _endpoint;
    private readonly ILogger<ActivityForwarder> _log;
    private readonly System.Threading.Channels.Channel<ActivityEvent> _queue
        = System.Threading.Channels.Channel.CreateUnbounded<ActivityEvent>();

    public ActivityForwarder(IHttpClientFactory http, IConfiguration cfg, ILogger<ActivityForwarder> log)
    {
        _httpFactory = http;
        _endpoint = cfg["Harness:IngestEndpoint"] ?? "http://localhost:4000/api/_ingest";
        _log = log;
        ActivityStream.Instance.OnEvent += ev => _queue.Writer.TryWrite(ev);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(3);
        await foreach (var ev in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await http.PostAsJsonAsync(_endpoint, ev, stoppingToken); }
            catch { /* best effort */ }
        }
    }
}
