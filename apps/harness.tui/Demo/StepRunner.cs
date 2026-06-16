namespace YourCustomAgentHarness.Tui.Demo;

using System.Diagnostics;
using System.Text;
using Spectre.Console;
using YourCustomAgentHarness.Tui;
using YourCustomAgentHarness.Tui.Runtime;

public enum StepOutcome { Completed, Skipped, Aborted }

public sealed class StepRunner
{
    private readonly IAnsiConsole _con;
    private readonly DemoState _state;
    private readonly bool _globalScripted;

    public StepRunner(IAnsiConsole con, DemoState state, bool globalScripted)
    {
        _con = con;
        _state = state;
        _globalScripted = globalScripted;
    }

    public async Task<StepOutcome> RunAsync(Step step, CancellationToken ct)
    {
        _con.WriteLine();
        Theme.Rule(_con, $"chapter {step.Chapter} · step {step.Number} · {step.ChapterTitle.ToLowerInvariant()}", Theme.Pulse);

        RenderIntro(step);
        if (!step.AutoAdvance) Pause("press enter to run the command…");

        var initialMode = _globalScripted ? StepMode.Scripted : step.DefaultMode;

        while (true)
        {
            // pane 2 + 3: command + execution
            var outcome = await ExecuteAsync(step, initialMode, ct);
            if (outcome == StepOutcome.Aborted) return outcome;

            // pane 4: confirm / bullets
            RenderBullets(step);

            // pane 5: portal menu (always optional)
            if (step.PortalLinks.Length > 0)
                ShowPortalMenu(step);

            if (!step.AutoAdvance) Pause("press enter for the next step…");
            return outcome;
        }
    }

    // ── panels ──────────────────────────────────────────────────────────

    private void RenderIntro(Step step)
    {
        var head = new Markup($"[{Theme.Neon}]{step.Title.EscapeMarkup()}[/]\n\n[{Theme.Bone}]{step.Intro}[/]");
        _con.Write(Theme.Panel($"[step {step.Number}]", head, Theme.Neon));
    }

    private void RenderBullets(Step step)
    {
        if (step.BulletPoints.Length == 0) return;
        var lines = string.Join("\n", step.BulletPoints.Select(b => $"  [{Theme.Cyan}]›[/] {b}"));
        _con.Write(Theme.Panel("what to point out", new Markup(lines), Theme.Cyan));
    }

    private void ShowPortalMenu(Step step)
    {
        _con.WriteLine();
        _con.MarkupLine($"[{Theme.Pulse}]portals to show this step[/] [{Theme.Steel}](press number to open · enter to skip)[/]");
        for (var i = 0; i < step.PortalLinks.Length; i++)
        {
            var p = step.PortalLinks[i];
            _con.MarkupLine($"  [{Theme.Amber}][[{i + 1}]][/] [{Theme.Bone}]{p.Title.EscapeMarkup()}[/]  [{Theme.Steel}]{p.Note.EscapeMarkup()}[/]");
            _con.MarkupLine($"        [{Theme.Steel}]{p.Url.EscapeMarkup()}[/]");
        }
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) return;
            if (key.KeyChar >= '1' && key.KeyChar <= '9')
            {
                var idx = key.KeyChar - '1';
                if (idx < step.PortalLinks.Length)
                {
                    OpenInBrowser(step.PortalLinks[idx].Url);
                    _con.MarkupLine($"  [{Theme.Acid}]→[/] opened [{Theme.Bone}]{step.PortalLinks[idx].Title.EscapeMarkup()}[/]");
                }
            }
        }
    }

    // ── execution & fallback ────────────────────────────────────────────

    private async Task<StepOutcome> ExecuteAsync(Step step, StepMode mode, CancellationToken ct)
    {
        if (mode == StepMode.Scripted || string.IsNullOrEmpty(step.Executable))
        {
            RenderCommandHeader(step, scripted: true);
            RenderScripted(step.ScriptedOutput);
            step.OnSuccess?.Invoke(_state, step.ScriptedOutput);
            return StepOutcome.Completed;
        }

        RenderCommandHeader(step, scripted: false);

        var (ok, output, err) = await SpawnAsync(step, ct);

        if (ok)
        {
            step.OnSuccess?.Invoke(_state, output);
            return StepOutcome.Completed;
        }

        // failure — fallback menu
        _con.MarkupLine($"\n[{Theme.Ember}]live command failed.[/] [{Theme.Steel}]({err.EscapeMarkup()})[/]\n");
        return await FallbackMenuAsync(step, ct);
    }

    private async Task<StepOutcome> FallbackMenuAsync(Step step, CancellationToken ct)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[{Theme.Amber}]how should we handle this?[/]")
                .HighlightStyle(Style.Parse(Theme.Neon))
                .AddChoices("[R]etry live", "[S]cripted output (recommended)", "S[k]ip this step", "[D]etails (last 60 log lines)", "[Q]uit demo"));

        if (choice.Contains("Retry")) return await ExecuteAsync(step, StepMode.Live, ct);
        if (choice.Contains("Scripted")) return await ExecuteAsync(step, StepMode.Scripted, ct);
        if (choice.Contains("Skip")) { _con.MarkupLine($"[{Theme.Amber}]step skipped.[/]"); return StepOutcome.Skipped; }
        if (choice.Contains("Details"))
        {
            ShowDetails();
            return await FallbackMenuAsync(step, ct);
        }
        return StepOutcome.Aborted;
    }

    private void ShowDetails()
    {
        var log = Path.Combine(HarnessPaths.LogsDir, "last-step.log");
        if (!File.Exists(log)) { _con.MarkupLine($"[{Theme.Steel}]no log captured[/]"); return; }
        var lines = File.ReadAllLines(log).TakeLast(60);
        _con.Write(Theme.Panel($"last-step.log (tail)", new Markup($"[{Theme.Steel}]{string.Join("\n", lines).EscapeMarkup()}[/]"), Theme.Steel));
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private void RenderCommandHeader(Step step, bool scripted)
    {
        var cmd = step.LiveCommand ?? (step.Executable != null ? $"{step.Executable} {string.Join(' ', step.Args)}" : "(no command)");
        var tag = scripted ? $"[{Theme.Amber}](scripted)[/]" : $"[{Theme.Acid}](live)[/]";
        _con.Write(Theme.Panel($"command {tag}", new Markup($"[{Theme.Bone}]$ {cmd.EscapeMarkup()}[/]"), scripted ? Theme.Amber : Theme.Acid));
    }

    private void RenderScripted(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            _con.MarkupLine($"[{Theme.Steel}](no scripted output for this step — talk through it)[/]");
            return;
        }
        foreach (var line in output.Split('\n'))
        {
            // very mild fake-typing: print whole line then small delay
            _con.MarkupLine($"[{Theme.Bone}]{line.TrimEnd('\r').EscapeMarkup()}[/]");
            Thread.Sleep(30);
        }
    }

    private async Task<(bool ok, string output, string error)> SpawnAsync(Step step, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var logPath = Path.Combine(HarnessPaths.LogsDir, "last-step.log");
        await using var log = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read));

        var psi = new ProcessStartInfo
        {
            FileName = step.Executable!,
            WorkingDirectory = step.WorkingDirectory ?? HarnessPaths.RepoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in step.Args) psi.ArgumentList.Add(a);

        Process proc;
        try { proc = Process.Start(psi)!; }
        catch (Exception ex) { return (false, "", $"could not start: {ex.Message}"); }

        var sw = Stopwatch.StartNew();
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            sb.AppendLine(e.Data);
            log.WriteLine(e.Data);
            _con.WriteLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            sb.AppendLine(e.Data);
            log.WriteLine("[err] " + e.Data);
            _con.MarkupLine($"[{Theme.Steel}][err][/] {e.Data.EscapeMarkup()}");
        };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(step.Timeout);
            await proc.WaitForExitAsync(linked.Token);
            sw.Stop();
            if (proc.ExitCode == 0)
            {
                _con.MarkupLine($"\n[{Theme.Acid}]✓ exit 0[/] [{Theme.Steel}]({sw.ElapsedMilliseconds} ms)[/]");
                return (true, sb.ToString(), "");
            }
            return (false, sb.ToString(), $"exit {proc.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return (false, sb.ToString(), $"timeout after {step.Timeout.TotalSeconds:F0}s");
        }
    }

    private void Pause(string prompt)
    {
        _con.MarkupLine($"\n[{Theme.Steel}]{prompt.EscapeMarkup()}[/]");
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter || k.Key == ConsoleKey.Spacebar) return;
            if (k.Key == ConsoleKey.Escape)
            {
                _con.MarkupLine($"[{Theme.Amber}](escape pressed — exit step? y/n)[/]");
                var c = Console.ReadKey(intercept: true);
                if (c.KeyChar is 'y' or 'Y') Environment.Exit(0);
            }
        }
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _con.MarkupLine($"[{Theme.Ember}]could not open browser: {ex.Message.EscapeMarkup()}[/]");
            _con.MarkupLine($"  copy manually: [{Theme.Cyan}]{url.EscapeMarkup()}[/]");
        }
    }
}
