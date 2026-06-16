namespace YourCustomAgentHarness.Tui.Runtime;

using System.Diagnostics;

public sealed record ProcessSpec(
    string Name,
    int Port,
    string HealthUrl,
    string Executable,
    string[] Args,
    string WorkingDirectory,
    Dictionary<string, string?>? Environment = null,
    string Category = "service",
    string Description = "")
{
    public string DisplayLine => $"{Name,-22} :{Port}   {Description}";
}

/// <summary>
/// Owns the lifecycle of every child process the harness spins up for the
/// workshop (api, web, two agents, kb-mcp). Uses Windows-friendly process-tree
/// kill semantics + per-process log files in <c>state/logs/</c>.
/// </summary>
public sealed class ProcessManager
{
    public static IReadOnlyList<ProcessSpec> Specs => _specs ??= BuildSpecs();
    private static IReadOnlyList<ProcessSpec>? _specs;

    private static IReadOnlyList<ProcessSpec> BuildSpecs()
    {
        var apps = HarnessPaths.AppsDir;

        // Resolve npm. `cmd /c npm ...` is the safe form on Windows because
        // npm ships as npm.cmd; calling it via cmd avoids the "is not a
        // recognized executable" classic.
        return new List<ProcessSpec>
        {
            new(
                Name: "harness.api",
                Port: 4000,
                HealthUrl: "http://localhost:4000/api/tenant",
                Executable: "dotnet",
                Args: new[] { "run", "--project", Path.Combine(apps, "harness.api", "harness.api.csproj"), "--no-launch-profile", "--urls", "http://localhost:4000" },
                WorkingDirectory: Path.Combine(apps, "harness.api"),
                Category: "control",
                Description: "admin API + SSE event stream"),

            new(
                Name: "harness.web",
                Port: 4001,
                HealthUrl: "http://localhost:4001/",
                Executable: "cmd",
                Args: new[] { "/c", "npm", "run", "start" },
                WorkingDirectory: Path.Combine(apps, "harness.web"),
                Category: "control",
                Description: "Next.js admin + hub UI (production server)"),

            new(
                Name: "customagentharness-kb-mcp",
                Port: 3981,
                HealthUrl: "http://localhost:3981/health",
                Executable: "dotnet",
                Args: new[] { "run", "--project", Path.Combine(apps, "customagentharness-kb-mcp", "customagentharness-kb-mcp.csproj"), "--no-launch-profile", "--urls", "http://localhost:3981" },
                WorkingDirectory: Path.Combine(apps, "customagentharness-kb-mcp"),
                Category: "runtime",
                Description: "local KB MCP server (15 bank policy docs)"),

            new(
                Name: "ForgedAgentOne",
                Port: 3979,
                HealthUrl: "http://localhost:3979/api/health",
                Executable: "dotnet",
                Args: new[] { "run", "--project", Path.Combine(apps, "ForgedAgentOne", "ForgedAgentOne.csproj"), "--no-launch-profile", "--urls", "http://localhost:3979" },
                WorkingDirectory: Path.Combine(apps, "ForgedAgentOne"),
                Category: "runtime",
                Description: "OBO/delegated agent — banker copilot"),

            new(
                Name: "ForgedScholarTwo",
                Port: 3980,
                HealthUrl: "http://localhost:3980/api/health",
                Executable: "dotnet",
                Args: new[] { "run", "--project", Path.Combine(apps, "ForgedScholarTwo", "ForgedScholarTwo.csproj"), "--no-launch-profile", "--urls", "http://localhost:3980" },
                WorkingDirectory: Path.Combine(apps, "ForgedScholarTwo"),
                Category: "runtime",
                Description: "app-perm agent — KB grounded answers"),
        };
    }

    /// <summary>
    /// Spawns the process, redirects stdout/stderr to a rolling log file and
    /// returns the resulting <see cref="ManagedProcess"/>. Does NOT wait for
    /// the service to become healthy — callers wait via <see cref="HealthChecker"/>.
    /// </summary>
    public ManagedProcess Spawn(ProcessSpec spec)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.Executable,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in spec.Args) psi.ArgumentList.Add(a);
        if (spec.Environment != null)
        {
            foreach (var kv in spec.Environment)
            {
                if (kv.Value is null) psi.Environment.Remove(kv.Key);
                else psi.Environment[kv.Key] = kv.Value;
            }
        }

        var logPath = Path.Combine(HarnessPaths.LogsDir, $"{spec.Name}.log");
        // Truncate previous log so each demo run starts fresh
        File.WriteAllText(logPath, $"== {spec.Name} started {DateTimeOffset.Now:O} ==\n");
        var writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) writer.WriteLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) writer.WriteLine("[err] " + e.Data); };
        proc.Exited += (_, _) =>
        {
            try
            {
                writer.WriteLine($"== exited {DateTimeOffset.Now:O} code={proc.ExitCode} ==");
                writer.Dispose();
            }
            catch { }
        };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        return new ManagedProcess
        {
            Name = spec.Name,
            Pid = proc.Id,
            Port = spec.Port,
            HealthUrl = spec.HealthUrl,
            LogPath = logPath,
            StartedAt = DateTimeOffset.UtcNow,
            Live = proc,
        };
    }
}
