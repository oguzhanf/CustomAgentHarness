namespace YourCustomAgentHarness.Tui.Commands;

using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using YourCustomAgentHarness.Tui.Runtime;

/// <summary>
/// <c>harness setup</c> — the single front door for getting an environment ready:
/// checks (and offers to install) prerequisites, writes the <c>.env</c>, ensures the
/// operator's Entra roles, provisions the blueprints + agent identities via the
/// <c>a365</c> CLI, and creates the Purview DLP policy. Every step is idempotent and
/// individually skippable, so it is safe to re-run.
/// </summary>
public sealed class SetupCommand : AsyncCommand<SetupCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-y|--yes")]
        [Description("Non-interactive: accept every step and skip prompts (best-effort).")]
        public bool Yes { get; init; }

        [CommandOption("--only <STEPS>")]
        [Description("Run only these comma-separated steps: prereqs,login,config,roles,provision,purview.")]
        public string? Only { get; init; }

        [CommandOption("--skip <STEPS>")]
        [Description("Skip these comma-separated steps.")]
        public string? Skip { get; init; }
    }

    private enum Outcome { Ok, Skipped, Warn, Failed }
    private sealed record StepResult(Outcome Outcome, string Detail);

    public override async Task<int> ExecuteAsync(CommandContext context, Settings s)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "guided setup · prerequisites → config → roles → provision → purview", Theme.Neon);
        con.MarkupLine($"[{Theme.Steel}]Idempotent and re-runnable. Use [/][{Theme.Bone}]--only[/]/[{Theme.Bone}]--skip[/][{Theme.Steel}] to target steps, [/][{Theme.Bone}]--yes[/][{Theme.Steel}] for non-interactive.[/]");
        con.WriteLine();

        var only = Parse(s.Only);
        var skip = Parse(s.Skip);
        bool Want(string step) => (only.Count == 0 || only.Contains(step)) && !skip.Contains(step);

        var results = new List<(string step, StepResult res)>();

        if (Want("prereqs"))   results.Add(("prerequisites",   await StepPrereqsAsync(con, s)));
        if (Want("login"))     results.Add(("sign in (az)",    await StepLoginAsync(con, s)));
        if (Want("config"))    results.Add(("configure .env",  await StepConfigAsync(con, s)));
        if (Want("roles"))     results.Add(("entra roles",     await StepScriptAsync(con, s,
            "grant-agent-roles.ps1", new[] { "-IncludePurview" },
            "Ensure the signed-in operator holds Agent ID + Agent Registry (+ Compliance) roles?")));
        if (Want("provision")) results.Add(("provision agents", await StepScriptAsync(con, s,
            "provision-agents.ps1", Array.Empty<string>(),
            "Create blueprints + mint agent identities via the a365 CLI? (an interactive sign-in may appear)")));
        if (Want("purview"))   results.Add(("purview dlp",     await StepScriptAsync(con, s,
            "create-purview-policies.ps1", Array.Empty<string>(),
            "Create the AI-app-scoped Purview DLP policy? (interactive Connect-IPPSSession sign-in)")));

        // ── summary ──
        con.WriteLine();
        Theme.Rule(con, "setup summary", Theme.Pulse);
        var table = new Table().BorderColor(Color.Grey39).Border(TableBorder.Rounded);
        table.AddColumn("[bold]step[/]");
        table.AddColumn("[bold]result[/]");
        table.AddColumn("[bold]detail[/]");
        foreach (var (step, res) in results)
            table.AddRow(step, Badge(res.Outcome), res.Detail.EscapeMarkup());
        con.Write(table);

        var failed = results.Count(r => r.res.Outcome == Outcome.Failed);
        con.WriteLine();
        if (failed == 0)
            con.MarkupLine($"[{Theme.Acid}]Setup finished.[/] Next: [{Theme.Bone}]harness up[/] then open [{Theme.Cyan}]http://localhost:4001[/]. Verify anytime with [{Theme.Bone}]harness doctor[/].");
        else
            con.MarkupLine($"[{Theme.Amber}]Setup finished with {failed} failed step(s).[/] Re-run the failed step, e.g. [{Theme.Bone}]harness setup --only provision[/].");
        return failed == 0 ? 0 : 1;
    }

    // ── Step 1: prerequisites ───────────────────────────────────────────────
    private async Task<StepResult> StepPrereqsAsync(IAnsiConsole con, Settings s)
    {
        Theme.Rule(con, "1 · prerequisites", Theme.Cyan);
        var rows = new List<(string tool, bool ok, string detail)>();

        rows.Add((".NET SDK", true, $"running on {Environment.Version}"));

        var pwsh = ResolvePwsh();
        var pwshOk = pwsh != null;
        rows.Add(("PowerShell 7", pwshOk, pwshOk ? pwsh! : "not found — install from https://aka.ms/powershell"));

        var az = Probes.ResolveAz();
        var (azOk, azOut, _) = await Probes.RunAsync(az, new[] { "version", "--output", "json" }, TimeSpan.FromSeconds(15), default);
        rows.Add(("Azure CLI", azOk, azOk ? "installed" : "not found — install from https://aka.ms/azcli"));

        var node = FindOnPath("node");
        rows.Add(("Node.js (web UI)", node != null, node ?? "not found — install Node 20+ (only needed for harness.web)"));

        var a365 = Probes.ResolveA365();
        rows.Add(("Agent 365 CLI (a365)", a365 != null, a365 ?? "missing"));

        var exoOk = pwshOk && await ModuleInstalledAsync(pwsh!, "ExchangeOnlineManagement");
        rows.Add(("ExchangeOnlineManagement", exoOk, exoOk ? "installed" : "missing (needed for Purview DLP)"));

        var table = new Table().BorderColor(Color.Grey39).Border(TableBorder.Rounded);
        table.AddColumn("[bold]tool[/]"); table.AddColumn("[bold]status[/]"); table.AddColumn("[bold]detail[/]");
        foreach (var (tool, ok, detail) in rows)
            table.AddRow(tool, ok ? $"[{Theme.Acid}]ok[/]" : $"[{Theme.Ember}]missing[/]", detail.EscapeMarkup());
        con.Write(table);

        // Offer installs for the two that we can install automatically.
        if (a365 == null && Confirm(con, s, "Install the Agent 365 CLI now (dotnet tool install -g)?"))
        {
            con.MarkupLine($"[{Theme.Steel}]› dotnet tool install --global Microsoft.Agents.A365.DevTools.Cli[/]");
            await RunInteractiveAsync("dotnet", new[] { "tool", "install", "--global", "Microsoft.Agents.A365.DevTools.Cli" });
            a365 = Probes.ResolveA365();
        }
        if (!exoOk && pwshOk && Confirm(con, s, "Install the ExchangeOnlineManagement PowerShell module now (CurrentUser)?"))
        {
            con.MarkupLine($"[{Theme.Steel}]› Install-Module ExchangeOnlineManagement -Scope CurrentUser -Force[/]");
            await RunInteractiveAsync(pwsh!, new[] { "-NoProfile", "-Command", "Install-Module ExchangeOnlineManagement -Scope CurrentUser -Force -AllowClobber" });
            exoOk = await ModuleInstalledAsync(pwsh!, "ExchangeOnlineManagement");
        }

        var hardMissing = !pwshOk || !azOk;
        if (hardMissing) return new StepResult(Outcome.Failed, "Install PowerShell 7 and/or Azure CLI, then re-run.");
        var softMissing = a365 == null || !exoOk || node == null;
        return softMissing
            ? new StepResult(Outcome.Warn, "Some optional tools missing (a365 / ExchangeOnline / node) — needed only for LIVE steps.")
            : new StepResult(Outcome.Ok, "All prerequisites present.");
    }

    // ── Step 2: az login ────────────────────────────────────────────────────
    private async Task<StepResult> StepLoginAsync(IAnsiConsole con, Settings s)
    {
        Theme.Rule(con, "2 · sign in (Azure CLI)", Theme.Cyan);
        var az = Probes.ResolveAz();
        var (ok, outJson, _) = await Probes.RunAsync(az, new[] { "account", "show", "--output", "json" }, TimeSpan.FromSeconds(10), default);
        if (ok)
        {
            var who = TryJson(outJson, "user", "name") ?? "(unknown)";
            var tid = TryJson(outJson, "tenantId") ?? "(unknown)";
            con.MarkupLine($"[{Theme.Acid}]Already signed in[/] as [{Theme.Bone}]{who.EscapeMarkup()}[/] (tenant {tid.EscapeMarkup()}).");
            return new StepResult(Outcome.Ok, $"{who}");
        }
        if (!Confirm(con, s, "Not signed in to Azure CLI. Run `az login` now?"))
            return new StepResult(Outcome.Skipped, "az login skipped — LIVE steps will fail until you sign in.");
        con.MarkupLine($"[{Theme.Steel}]› az login[/]");
        var code = await RunInteractiveAsync(az, new[] { "login" });
        return code == 0 ? new StepResult(Outcome.Ok, "signed in") : new StepResult(Outcome.Failed, "az login failed");
    }

    // ── Step 3: configure .env ──────────────────────────────────────────────
    private async Task<StepResult> StepConfigAsync(IAnsiConsole con, Settings s)
    {
        Theme.Rule(con, "3 · configure .env", Theme.Cyan);
        var root = HarnessPaths.RepoRoot;
        var envPath = Path.Combine(root, ".env");
        var examplePath = Path.Combine(root, ".env.example");

        if (!File.Exists(envPath))
        {
            if (File.Exists(examplePath)) { File.Copy(examplePath, envPath); con.MarkupLine($"[{Theme.Acid}]Created[/] .env from .env.example."); }
            else { File.WriteAllText(envPath, ""); con.MarkupLine($"[{Theme.Amber}].env.example not found — created an empty .env.[/]"); }
        }
        else con.MarkupLine($"[{Theme.Steel}].env already exists — updating empty keys only.[/]");

        // Auto-detect sensible defaults from the signed-in az context.
        var az = Probes.ResolveAz();
        var (ok, acct, _) = await Probes.RunAsync(az, new[] { "account", "show", "--output", "json" }, TimeSpan.FromSeconds(10), default);
        var tenantDefault = ok ? TryJson(acct, "tenantId") : null;
        var upnDefault = ok ? TryJson(acct, "user", "name") : null;
        var subDefault = ok ? TryJson(acct, "id") : null;

        if (s.Yes)
        {
            // Non-interactive: only fill what we can detect, leave the rest for the user.
            if (tenantDefault != null) SetEnvKey(envPath, "TENANT_ID", tenantDefault, onlyIfEmpty: true);
            if (upnDefault != null) SetEnvKey(envPath, "ADMIN_UPN", upnDefault, onlyIfEmpty: true);
            if (subDefault != null) SetEnvKey(envPath, "SUBSCRIPTION_ID", subDefault, onlyIfEmpty: true);
            return new StepResult(Outcome.Ok, "auto-filled tenant/admin/subscription from az (where empty).");
        }

        con.MarkupLine($"[{Theme.Steel}]Press Enter to keep the shown default / current value.[/]");
        Ask(con, envPath, "TENANT_ID", "Entra tenant id", tenantDefault);
        Ask(con, envPath, "ADMIN_UPN", "Operator / blueprint-owner UPN", upnDefault);
        Ask(con, envPath, "SUBSCRIPTION_ID", "Azure subscription id", subDefault);
        Ask(con, envPath, "FOUNDRY_ACCOUNT", "Foundry / AI Services account name", null);
        Ask(con, envPath, "FOUNDRY_RESOURCE_GROUP", "Foundry resource group", null);
        Ask(con, envPath, "FOUNDRY_ENDPOINT", "Foundry endpoint (https://<acct>.cognitiveservices.azure.com/)", null);
        con.MarkupLine($"[{Theme.Acid}]Saved[/] to {envPath.EscapeMarkup()}. (App ids are filled after provisioning.)");
        return new StepResult(Outcome.Ok, "configured");
    }

    // ── Step 4-6: run a workshop script interactively ───────────────────────
    private async Task<StepResult> StepScriptAsync(IAnsiConsole con, Settings s, string script, string[] args, string prompt)
    {
        var title = Path.GetFileNameWithoutExtension(script);
        Theme.Rule(con, $"· {title}", Theme.Cyan);
        var pwsh = ResolvePwsh();
        if (pwsh == null) return new StepResult(Outcome.Failed, "PowerShell 7 (pwsh) not found.");
        var scriptPath = Path.Combine(HarnessPaths.WorkshopDir, "scripts", script);
        if (!File.Exists(scriptPath)) return new StepResult(Outcome.Failed, $"{script} not found.");
        if (!Confirm(con, s, prompt)) return new StepResult(Outcome.Skipped, "skipped by operator.");

        con.MarkupLine($"[{Theme.Steel}]› pwsh {script} {string.Join(' ', args)}[/]");
        var full = new List<string> { "-NoProfile", "-File", scriptPath };
        full.AddRange(args);
        var code = await RunInteractiveAsync(pwsh, full, HarnessPaths.RepoRoot);
        return code == 0
            ? new StepResult(Outcome.Ok, "completed")
            : new StepResult(Outcome.Warn, $"exited {code} — review output above (a 403 on registration usually means preview enrollment).");
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    private static HashSet<string> Parse(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Badge(Outcome o) => o switch
    {
        Outcome.Ok => $"[{Theme.Acid}]ok[/]",
        Outcome.Skipped => $"[{Theme.Steel}]skipped[/]",
        Outcome.Warn => $"[{Theme.Amber}]warn[/]",
        _ => $"[{Theme.Ember}]failed[/]",
    };

    private static bool Confirm(IAnsiConsole con, Settings s, string prompt) =>
        s.Yes || con.Confirm($"[{Theme.Bone}]{prompt.EscapeMarkup()}[/]");

    private static void Ask(IAnsiConsole con, string envPath, string key, string label, string? def)
    {
        var current = GetEnvKey(envPath, key);
        var shown = !string.IsNullOrWhiteSpace(current) ? current : def;
        var promptText = shown is null ? $"{label} [{key}]" : $"{label} [{key}] ({shown})";
        var answer = con.Prompt(new TextPrompt<string>(promptText.EscapeMarkup()).AllowEmpty());
        var value = string.IsNullOrWhiteSpace(answer) ? (shown ?? "") : answer;
        if (!string.IsNullOrWhiteSpace(value)) SetEnvKey(envPath, key, value, onlyIfEmpty: false);
    }

    private static string? GetEnvKey(string path, string key)
    {
        if (!File.Exists(path)) return null;
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.TrimStart();
            if (t.StartsWith('#')) continue;
            var eq = t.IndexOf('=');
            if (eq > 0 && t[..eq].Trim().Equals(key, StringComparison.Ordinal))
                return CleanVal(t[(eq + 1)..]);
        }
        return null;
    }

    /// <summary>Extract a real value, dropping inline comments (`KEY=   # hint` → "") and quotes.</summary>
    private static string CleanVal(string raw)
    {
        var v = raw.Trim();
        if (v.StartsWith('#')) return "";
        if (v.Length > 0 && v[0] != '"' && v[0] != '\'')
        {
            var h = v.IndexOf(" #", StringComparison.Ordinal);
            if (h >= 0) v = v[..h].Trim();
        }
        if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
            v = v[1..^1];
        return v;
    }

    /// <summary>Update <c>KEY=value</c> in place (preserving comments), or append it.</summary>
    private static void SetEnvKey(string path, string key, string value, bool onlyIfEmpty)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith('#')) continue;
            var eq = t.IndexOf('=');
            if (eq > 0 && t[..eq].Trim().Equals(key, StringComparison.Ordinal))
            {
                var existing = CleanVal(t[(eq + 1)..]);
                if (onlyIfEmpty && !string.IsNullOrWhiteSpace(existing)) return;
                lines[i] = $"{key}={value}";
                File.WriteAllLines(path, lines);
                return;
            }
        }
        lines.Add($"{key}={value}");
        File.WriteAllLines(path, lines);
    }

    private static async Task<int> RunInteractiveAsync(string exe, IEnumerable<string> args, string? workingDir = null)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = false, CreateNoWindow = false };
        if (workingDir != null) psi.WorkingDirectory = workingDir;
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[{Theme.Ember}]could not run {exe.EscapeMarkup()}: {ex.Message.EscapeMarkup()}[/]");
            return -1;
        }
    }

    private static async Task<bool> ModuleInstalledAsync(string pwsh, string module)
    {
        var (ok, outp, _) = await Probes.RunAsync(pwsh,
            new[] { "-NoProfile", "-Command", $"if (Get-Module -ListAvailable -Name {module}) {{ 'yes' }} else {{ 'no' }}" },
            TimeSpan.FromSeconds(20), default);
        return ok && outp.Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolvePwsh()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerShell\7\pwsh.exe"),
            @"C:\Program Files\PowerShell\7\pwsh.exe",
        };
        return candidates.FirstOrDefault(File.Exists) ?? FindOnPath("pwsh");
    }

    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            foreach (var ext in new[] { ".exe", ".cmd", ".bat", "" })
            {
                try { var full = Path.Combine(dir, name + ext); if (File.Exists(full)) return full; } catch { }
            }
        }
        return null;
    }

    private static string? TryJson(string json, params string[] path)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var cur = doc.RootElement;
            foreach (var p in path)
            {
                if (cur.ValueKind != System.Text.Json.JsonValueKind.Object || !cur.TryGetProperty(p, out cur)) return null;
            }
            return cur.ValueKind == System.Text.Json.JsonValueKind.String ? cur.GetString() : cur.ToString();
        }
        catch { return null; }
    }
}
