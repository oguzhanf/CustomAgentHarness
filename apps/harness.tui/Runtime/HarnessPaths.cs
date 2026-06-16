namespace YourCustomAgentHarness.Tui.Runtime;

/// <summary>
/// File-system paths used by the harness. Resolved once at startup against the
/// current working directory; falls back to the assembly location for unusual
/// invocations (e.g. <c>harness.exe</c> dragged onto desktop).
/// </summary>
public static class HarnessPaths
{
    /// <summary>The root of the repo — folder that contains <c>YourCustomAgentHarness.sln</c>.</summary>
    public static string RepoRoot { get; }

    public static string StateDir => Path.Combine(RepoRoot, "state");
    public static string LogsDir
    {
        get
        {
            var d = Path.Combine(StateDir, "logs");
            Directory.CreateDirectory(d);
            return d;
        }
    }
    public static string TenantStateYaml => Path.Combine(RepoRoot, "tenant-state.yaml");
    public static string DemoStateJson => Path.Combine(StateDir, "demo-state.json");

    public static string AppsDir => Path.Combine(RepoRoot, "apps");
    public static string BlueprintsDir => Path.Combine(RepoRoot, "blueprints");
    public static string WorkshopDir => Path.Combine(RepoRoot, "workshop");

    static HarnessPaths()
    {
        RepoRoot = LocateRepoRoot();
        Directory.CreateDirectory(StateDir);
    }

    private static string LocateRepoRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        var fromCwd = WalkUp(cwd);
        if (fromCwd != null) return fromCwd;

        var asm = Path.GetDirectoryName(typeof(HarnessPaths).Assembly.Location);
        if (asm != null)
        {
            var fromAsm = WalkUp(asm);
            if (fromAsm != null) return fromAsm;
        }
        return cwd;
    }

    private static string? WalkUp(string start)
    {
        var d = new DirectoryInfo(start);
        for (var i = 0; i < 10 && d != null; i++, d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "YourCustomAgentHarness.sln")))
                return d.FullName;
        }
        return null;
    }
}
