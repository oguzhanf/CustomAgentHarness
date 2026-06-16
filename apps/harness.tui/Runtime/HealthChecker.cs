namespace YourCustomAgentHarness.Tui.Runtime;

using System.Net.Sockets;
using System.Text.Json;

/// <summary>
/// Port + HTTP health probes. Kept dependency-free so it can run before
/// HttpClient configuration in the DI container.
/// </summary>
public static class HealthChecker
{
    public static async Task<bool> PortInUseAsync(int port, int timeoutMs = 800)
    {
        try
        {
            using var c = new TcpClient();
            var connect = c.ConnectAsync("127.0.0.1", port);
            var timeout = Task.Delay(timeoutMs);
            await Task.WhenAny(connect, timeout);
            return c.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<HealthProbe> HttpAsync(string url, int timeoutMs = 1500)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            using var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            return new HealthProbe(true, (int)resp.StatusCode, Trim(body, 200), null);
        }
        catch (Exception ex)
        {
            return new HealthProbe(false, 0, null, ex.Message);
        }
    }

    public static async Task<bool> WaitForHttpReadyAsync(string url, TimeSpan max, TimeSpan? interval = null)
    {
        var ivl = interval ?? TimeSpan.FromMilliseconds(750);
        var until = DateTimeOffset.UtcNow + max;
        while (DateTimeOffset.UtcNow < until)
        {
            var probe = await HttpAsync(url, 1500);
            if (probe.Ok && probe.StatusCode is >= 200 and < 500) return true;
            await Task.Delay(ivl);
        }
        return false;
    }

    public static IReadOnlyDictionary<string, string> ParseHealthSummary(string? body)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(body)) return dict;
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) dict[p.Name] = p.Value.GetString() ?? "";
                else if (p.Value.ValueKind == JsonValueKind.Number) dict[p.Name] = p.Value.ToString();
                else if (p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False) dict[p.Name] = p.Value.GetBoolean().ToString();
            }
        }
        catch { /* not JSON */ }
        return dict;
    }

    private static string Trim(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n) + "…";
}

public sealed record HealthProbe(bool Ok, int StatusCode, string? Body, string? Error);
