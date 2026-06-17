namespace YourCustomAgentHarness.Shared;

/// <summary>
/// Minimal <c>.env</c> loader so the harness has a single, documented place for
/// environment-specific values (tenant ids, foundry endpoint, agent app ids, Purview
/// settings, model key). Call <see cref="Load"/> once at process start, BEFORE building
/// configuration — values are pushed into the process environment, where ASP.NET Core's
/// configuration (and the rest of the harness) picks them up automatically.
/// </summary>
/// <remarks>
/// Rules: <c>KEY=VALUE</c> per line · <c>#</c> starts a comment · blank lines ignored ·
/// optional surrounding single/double quotes are stripped · an optional <c>export </c>
/// prefix is allowed · a real process env var ALWAYS wins over the file (so CI / shells can override).
/// The file is searched for from the start directory upward to the repo root.
/// </remarks>
public static class DotEnv
{
    /// <summary>Load the nearest <c>.env</c> (searching up from <paramref name="startDir"/>). Returns the path loaded, or null.</summary>
    public static string? Load(string? startDir = null)
    {
        var path = Resolve(startDir);
        if (path is null) return null;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) line = line[7..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            // A value that is only an inline comment (e.g. `KEY=   # hint` from .env.example) is EMPTY.
            if (value.StartsWith('#'))
            {
                value = "";
            }
            else if (value.Length > 0 && value[0] != '"' && value[0] != '\'')
            {
                // strip a trailing inline comment that is not inside quotes
                var hash = value.IndexOf(" #", StringComparison.Ordinal);
                if (hash >= 0) value = value[..hash].Trim();
            }
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Real environment variables win — never clobber an already-set value.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
        return path;
    }

    /// <summary>Find the nearest <c>.env</c> from <paramref name="startDir"/> up to the filesystem root.</summary>
    public static string? Resolve(string? startDir = null)
    {
        var dir = startDir ?? Directory.GetCurrentDirectory();
        for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
        }
        // Fall back to walking up from the executing assembly location too (covers `dotnet run` from app dir).
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, ".env");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
