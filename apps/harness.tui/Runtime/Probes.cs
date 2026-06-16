namespace YourCustomAgentHarness.Tui.Runtime;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Doctor-style probes the TUI runs to decide whether the demo can go LIVE
/// or must fall back to SCRIPTED for individual steps. All probes are
/// non-destructive and short-timeout.
/// </summary>
public static class Probes
{
    public static async Task<ProbeResult> AzLoggedInAsync(CancellationToken ct = default)
    {
        var (ok, stdout, stderr) = await RunAsync(ResolveAz(), new[] { "account", "show", "--output", "json" }, TimeSpan.FromSeconds(8), ct);
        if (!ok) return ProbeResult.Fail("az", $"`az account show` failed: {TrimErr(stderr)}");
        return ProbeResult.Pass("az", Summarise(stdout, "user.name", "tenantId", "name"));
    }

    public static async Task<ProbeResult> A365InstalledAsync(CancellationToken ct = default)
    {
        var a365 = ResolveA365();
        if (a365 == null) return ProbeResult.Fail("a365", "`a365` CLI not on PATH. Run `dotnet tool install -g Microsoft.Agents.A365.DevTools.Cli`.");
        var (ok, stdout, _) = await RunAsync(a365, new[] { "--version" }, TimeSpan.FromSeconds(4), ct);
        return ok
            ? ProbeResult.Pass("a365", $"v{stdout.Trim()}")
            : ProbeResult.Fail("a365", "`a365 --version` did not respond. CLI may be broken.");
    }

    public static async Task<ProbeResult> FoundryReachableAsync(string endpoint, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(endpoint)) return ProbeResult.Fail("foundry", "endpoint missing from tenant-state.yaml");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(endpoint.TrimEnd('/'), ct);
            // Any HTTP response = reachable. Auth fail is expected here (we have no key).
            return ProbeResult.Pass("foundry", $"reachable ({(int)resp.StatusCode})");
        }
        catch (Exception ex)
        {
            return ProbeResult.Fail("foundry", $"unreachable: {ex.GetType().Name}");
        }
    }

    public static async Task<ProbeResult> ApiKeyConfiguredAsync(CancellationToken ct = default)
    {
        // Prefer Entra-auth: Foundry usually has disableLocalAuth=true. We try to acquire
        // a token for cognitiveservices.azure.com via DefaultAzureCredential (which picks
        // up `az login`). If that works we PASS — the agents will use the same credential
        // path. Otherwise we fall back to checking for a configured api key.
        try
        {
            var cred = new Azure.Identity.DefaultAzureCredential(new Azure.Identity.DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = true,
                ExcludeVisualStudioCredential = false,
                ExcludeAzureCliCredential = false,
            });
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(25));
            var tok = await cred.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }), cts.Token);
            if (!string.IsNullOrEmpty(tok.Token))
                return ProbeResult.Pass("aoai-auth", "DefaultAzureCredential token acquired for cognitiveservices.azure.com");
        }
        catch { /* fall through to key check */ }

        var env = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return ProbeResult.Pass("aoai-auth", "AZURE_OPENAI_API_KEY (env) set");

        var path = Path.Combine(HarnessPaths.AppsDir, "ForgedAgentOne", "appsettings.json");
        if (!File.Exists(path)) return ProbeResult.Fail("aoai-auth", "no Entra token, no env var, no appsettings.json");
        var raw = await File.ReadAllTextAsync(path, ct);
        if (raw.Contains("\"ApiKey\": \"\""))
            return ProbeResult.Fail("aoai-auth", "Entra-auth failed AND AzureOpenAI:ApiKey is empty. Run `az login` (MFA may be required) so DefaultAzureCredential can acquire a token.");
        return ProbeResult.Pass("aoai-auth", "AzureOpenAI:ApiKey present in appsettings.json");
    }

    public static async Task<ProbeResult> PurviewReadyAsync(CancellationToken ct = default)
    {
        await Task.Yield();
        // Heuristic: requires that ForgedAgentOne appsettings has AppLocationValue populated
        // AND that PowerShell + ExchangeOnlineManagement module are reachable. We don't
        // actually call Graph here because that would need an admin token round-trip.
        var aoSet = AppLocationValueFilled("ForgedAgentOne");
        var fsSet = AppLocationValueFilled("ForgedScholarTwo");

        if (!aoSet && !fsSet)
            return ProbeResult.Fail("purview",
                "AppLocationValue empty in both agents' appsettings.json. " +
                "Run `a365 setup blueprint --agent-name ForgedAgentOne` (and Two), then " +
                "fill in each agent's Entra app id in appsettings.json. " +
                "Until then the agents will fall back to the regex SIT classifier.");

        if (!aoSet || !fsSet)
            return ProbeResult.Warn("purview", $"Partial: ForgedAgentOne={(aoSet ? "set" : "empty")}, ForgedScholarTwo={(fsSet ? "set" : "empty")}");

        return ProbeResult.Pass("purview", "AppLocationValue populated for both agents. Demo will attempt Graph (Auto mode).");
    }

    private static bool AppLocationValueFilled(string agentName)
    {
        var path = Path.Combine(HarnessPaths.AppsDir, agentName, "appsettings.json");
        if (!File.Exists(path)) return false;
        var raw = File.ReadAllText(path);
        // Crude: look for non-empty AppLocationValue
        var idx = raw.IndexOf("\"AppLocationValue\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        var colon = raw.IndexOf(':', idx);
        var openQ = raw.IndexOf('"', colon + 1);
        var closeQ = raw.IndexOf('"', openQ + 1);
        if (openQ < 0 || closeQ < 0 || closeQ - openQ <= 1) return false;
        return true;
    }

    // ── command helpers ──

    public static string? ResolveA365()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.dotnet\tools\a365.exe"),
            Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.dotnet\tools\a365.cmd"),
            Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\.dotnet\tools\a365"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? FindOnPath("a365");
    }

    public static string ResolveAz()
    {
        var candidates = new[]
        {
            @"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
            @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd",
        };
        return candidates.FirstOrDefault(File.Exists) ?? "az";
    }

    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            foreach (var ext in new[] { ".exe", ".cmd", ".bat", "" })
            {
                var full = Path.Combine(dir, name + ext);
                if (File.Exists(full)) return full;
            }
        }
        return null;
    }

    public static async Task<(bool ok, string stdout, string stderr)> RunAsync(
        string exe, string[] args, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(timeout);
            await proc.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return (false, "", $"timeout after {timeout.TotalSeconds:F0}s");
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (proc.ExitCode == 0, stdout, stderr);
    }

    private static string Summarise(string json, params string[] paths)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var sb = new StringBuilder();
            foreach (var p in paths)
            {
                var v = ReadPath(doc.RootElement, p);
                if (v != null)
                {
                    if (sb.Length > 0) sb.Append(" · ");
                    sb.Append(v);
                }
            }
            return sb.Length == 0 ? "ok" : sb.ToString();
        }
        catch { return "ok"; }
    }

    private static string? ReadPath(System.Text.Json.JsonElement el, string dottedPath)
    {
        var cur = el;
        foreach (var part in dottedPath.Split('.'))
        {
            if (cur.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (!cur.TryGetProperty(part, out cur)) return null;
        }
        return cur.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => cur.GetString(),
            System.Text.Json.JsonValueKind.Number => cur.ToString(),
            _ => null
        };
    }

    private static string TrimErr(string s) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= 120 ? s.Trim() : s.Substring(0, 120).Trim() + "…";
}

public sealed record ProbeResult(string Name, ProbeStatus Status, string Detail)
{
    public static ProbeResult Pass(string n, string d) => new(n, ProbeStatus.Pass, d);
    public static ProbeResult Warn(string n, string d) => new(n, ProbeStatus.Warn, d);
    public static ProbeResult Fail(string n, string d) => new(n, ProbeStatus.Fail, d);
}

public enum ProbeStatus { Pass, Warn, Fail }
