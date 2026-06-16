namespace YourCustomAgentHarness.Tui.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using YourCustomAgentHarness.Tui;
using YourCustomAgentHarness.Tui.Runtime;

/// <summary>
/// <c>harness up</c> — starts every service the demo needs (api, web, kb-mcp,
/// both agents) and waits for each to become healthy. Idempotent: skips
/// services whose port is already serving.
/// </summary>
public sealed class UpCommand : AsyncCommand<UpCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--only <NAMES>")]
        public string? Only { get; init; }

        [CommandOption("--no-wait")]
        public bool NoWait { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "bringing the harness up", Theme.Neon);

        var only = settings.Only?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pm = new ProcessManager();
        var managed = ProcessStateStore.Load();
        var started = new List<ManagedProcess>(managed);

        foreach (var spec in ProcessManager.Specs)
        {
            if (only != null && !only.Contains(spec.Name)) continue;

            // 1. Port-already-in-use → assume someone (us, last run, the user) started it
            if (await HealthChecker.PortInUseAsync(spec.Port))
            {
                con.MarkupLine($"[{Theme.Acid}]✓[/] [{Theme.Steel}]:{spec.Port}[/] already serving — skipping [{Theme.Bone}]{spec.Name.EscapeMarkup()}[/]");
                continue;
            }

            con.MarkupLine($"[{Theme.Cyan}]→[/] starting [{Theme.Bone}]{spec.Name.EscapeMarkup()}[/] on :{spec.Port}");
            ManagedProcess proc;
            try
            {
                proc = pm.Spawn(spec);
            }
            catch (Exception ex)
            {
                con.MarkupLine($"[{Theme.Ember}]✗[/] [{Theme.Bone}]{spec.Name.EscapeMarkup()}[/] failed to spawn: [{Theme.Ember}]{ex.Message.EscapeMarkup()}[/]");
                continue;
            }
            started.Add(proc);
            ProcessStateStore.Save(started);

            if (!settings.NoWait && !string.IsNullOrEmpty(spec.HealthUrl))
            {
                con.Markup($"  [{Theme.Steel}]waiting for {spec.HealthUrl.EscapeMarkup()}…[/] ");
                var ready = await HealthChecker.WaitForHttpReadyAsync(spec.HealthUrl, TimeSpan.FromSeconds(60));
                if (ready)
                    con.MarkupLine($"[{Theme.Acid}]ready[/] (pid {proc.Pid})");
                else
                    con.MarkupLine($"[{Theme.Amber}]not responding after 60s[/] — check [link]{proc.LogPath?.EscapeMarkup()}[/]");
            }
        }

        ProcessStateStore.Save(started);

        con.WriteLine();
        Theme.Rule(con, "harness is up. try `harness status` or `harness demo`.", Theme.Pulse);
        return 0;
    }
}

/// <summary>
/// <c>harness down</c> — kills every process we started + their child trees,
/// using the PID list in <c>state/processes.json</c>. Tolerant: a process that
/// already exited is fine.
/// </summary>
public sealed class DownCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "shutting down managed processes", Theme.Amber);

        var managed = ProcessStateStore.Load();
        if (managed.Count == 0)
        {
            con.MarkupLine($"[{Theme.Steel}]no managed processes in state file ({ProcessStateStore.StatePath.EscapeMarkup()}); doing port-sweep anyway[/]");
        }

        foreach (var mp in managed)
        {
            var alive = mp.IsAlive();
            con.Markup($"  [{Theme.Steel}]{mp.Name.EscapeMarkup(),-26}[/] pid {mp.Pid,-6} ");
            if (!alive) { con.MarkupLine($"[{Theme.Steel}](already gone)[/]"); continue; }
            try
            {
                mp.Kill();
                await Task.Delay(120);
                con.MarkupLine($"[{Theme.Acid}]killed[/]");
            }
            catch (Exception ex)
            {
                con.MarkupLine($"[{Theme.Ember}]error: {ex.Message.EscapeMarkup()}[/]");
            }
        }

        // Port sweep: on Windows `npm.cmd` spawns a node child that often outlives
        // Process.Kill(entireProcessTree:true). Walk each managed port and kill
        // whatever is still listening on it.
        foreach (var spec in ProcessManager.Specs)
        {
            try
            {
                var conns = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Where(e => e.Port == spec.Port)
                    .ToList();
                if (conns.Count == 0) continue;

                // Find the OwningProcess via netstat -ano (TcpListener API doesn't expose PID)
                var psi = new System.Diagnostics.ProcessStartInfo("netstat", "-ano -p TCP")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi)!;
                var output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                foreach (var line in output.Split('\n'))
                {
                    var trim = line.Trim();
                    if (!trim.Contains($":{spec.Port} ") && !trim.Contains($":{spec.Port}\t")) continue;
                    if (!trim.Contains("LISTENING")) continue;
                    var parts = trim.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5) continue;
                    if (!int.TryParse(parts[^1], out var pid)) continue;
                    if (pid <= 4) continue; // system / idle
                    try
                    {
                        var p2 = System.Diagnostics.Process.GetProcessById(pid);
                        con.MarkupLine($"  [{Theme.Steel}]port {spec.Port,5}[/] sweeping leftover pid {pid} ([{Theme.Steel}]{p2.ProcessName.EscapeMarkup()}[/])");
                        p2.Kill(entireProcessTree: true);
                    }
                    catch { /* gone */ }
                }
            }
            catch { /* ignore — best-effort */ }
        }

        ProcessStateStore.Clear();
        con.WriteLine();
        Theme.Rule(con, "done.", Theme.Pulse);
        return 0;
    }
}

/// <summary>
/// <c>harness status</c> — port + process table showing what the harness
/// thinks is running. Independent of the demo state machine, so safe to call
/// any time.
/// </summary>
public sealed class StatusCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "harness status", Theme.Cyan);

        var managed = ProcessStateStore.Load().ToDictionary(m => m.Name);
        var table = new Table().BorderColor(Color.Grey39).Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]service[/]"));
        table.AddColumn(new TableColumn("[bold]port[/]"));
        table.AddColumn(new TableColumn("[bold]pid[/]"));
        table.AddColumn(new TableColumn("[bold]port up[/]"));
        table.AddColumn(new TableColumn("[bold]health[/]"));
        table.AddColumn(new TableColumn("[bold]log[/]"));

        foreach (var spec in ProcessManager.Specs)
        {
            managed.TryGetValue(spec.Name, out var mp);
            var portUp = await HealthChecker.PortInUseAsync(spec.Port);
            HealthProbe? health = null;
            if (!string.IsNullOrEmpty(spec.HealthUrl))
                health = await HealthChecker.HttpAsync(spec.HealthUrl, 1200);

            var pidCol = mp?.Pid.ToString() ?? "—";
            var portCol = portUp ? $"[{Theme.Acid}]●[/] {spec.Port}" : $"[{Theme.Ember}]○[/] {spec.Port}";
            string healthCol;
            if (health == null) healthCol = $"[{Theme.Steel}]n/a[/]";
            else if (health.Ok && health.StatusCode is >= 200 and < 500) healthCol = $"[{Theme.Acid}]{health.StatusCode}[/]";
            else healthCol = $"[{Theme.Ember}]{(health.Ok ? health.StatusCode.ToString() : "ERR")}[/]";
            var logCol = mp?.LogPath is { } lp ? $"[{Theme.Steel}]{Path.GetFileName(lp).EscapeMarkup()}[/]" : "—";

            table.AddRow(spec.Name, portCol, pidCol, portUp ? $"[{Theme.Acid}]yes[/]" : $"[{Theme.Ember}]no[/]", healthCol, logCol);
        }
        con.Write(table);

        var ts = TenantContext.Load();
        con.WriteLine();
        con.MarkupLine($"[{Theme.Steel}]tenant   :[/] {ts.Tenant.Domain.EscapeMarkup()} ({ts.Tenant.Id.EscapeMarkup()})");
        con.MarkupLine($"[{Theme.Steel}]admin    :[/] {ts.Tenant.Admin.EscapeMarkup()}");
        con.MarkupLine($"[{Theme.Steel}]foundry  :[/] {ts.Foundry.AccountName.EscapeMarkup()} ({ts.Foundry.Endpoint.EscapeMarkup()})");
        con.MarkupLine($"[{Theme.Steel}]state    :[/] {HarnessPaths.StateDir.EscapeMarkup()}");
        return 0;
    }
}

/// <summary>
/// <c>harness doctor</c> — pre-flight checks. Designed to be safe to run any
/// time, with explicit pass / warn / fail verdicts. Exits 0 unless something
/// would actively prevent the demo from happening at all.
/// </summary>
public sealed class DoctorCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "preflight checks", Theme.Cyan);

        var ts = TenantContext.Load();
        var results = new List<ProbeResult>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse(Theme.Pulse))
            .StartAsync("running probes…", async ctx =>
            {
                ctx.Status("az login");
                results.Add(await Probes.AzLoggedInAsync());
                ctx.Status("a365 cli");
                results.Add(await Probes.A365InstalledAsync());
                ctx.Status("foundry");
                results.Add(await Probes.FoundryReachableAsync(ts.Foundry.Endpoint));
                ctx.Status("aoai auth");
                results.Add(await Probes.ApiKeyConfiguredAsync());
                ctx.Status("purview");
                results.Add(await Probes.PurviewReadyAsync());
            });

        var failed = 0; var warned = 0;
        var table = new Table().BorderColor(Color.Grey39).Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[bold]probe[/]"));
        table.AddColumn(new TableColumn("[bold]status[/]"));
        table.AddColumn(new TableColumn("[bold]detail[/]"));
        foreach (var r in results)
        {
            string statusCell = r.Status switch
            {
                ProbeStatus.Pass => $"[{Theme.Acid}]PASS[/]",
                ProbeStatus.Warn => $"[{Theme.Amber}]WARN[/]",
                _ => $"[{Theme.Ember}]FAIL[/]",
            };
            if (r.Status == ProbeStatus.Fail) failed++;
            if (r.Status == ProbeStatus.Warn) warned++;
            table.AddRow(r.Name, statusCell, r.Detail.EscapeMarkup());
        }
        con.Write(table);
        con.WriteLine();
        con.MarkupLine($"[{Theme.Steel}]summary:[/] [{Theme.Acid}]{results.Count - failed - warned} pass[/], [{Theme.Amber}]{warned} warn[/], [{Theme.Ember}]{failed} fail[/]");

        if (failed > 0)
        {
            con.MarkupLine($"[{Theme.Ember}]The demo can still run but some live steps will fall back to scripted.[/]");
        }
        return 0;
    }
}
