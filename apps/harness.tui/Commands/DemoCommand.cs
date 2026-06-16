namespace YourCustomAgentHarness.Tui.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using YourCustomAgentHarness.Tui;
using YourCustomAgentHarness.Tui.Demo;
using YourCustomAgentHarness.Tui.Runtime;

/// <summary>
/// <c>harness demo</c> — the main workshop loop. Preflight → chapter menu →
/// step runner with per-step fallback. Per rubber-duck guidance this is
/// intentionally minimum-viable: it does not orchestrate provisioning, it
/// drives the [italic]story[/] of provisioning while the audience watches.
/// </summary>
public sealed class DemoCommand : AsyncCommand<DemoCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from-chapter <N>")]
        public int? FromChapter { get; init; }

        [CommandOption("--from-step <N>")]
        public int? FromStep { get; init; }

        [CommandOption("--demo-mode")]
        [System.ComponentModel.Description("Force every step to run in scripted mode regardless of step default.")]
        public bool ScriptedAll { get; init; }

        [CommandOption("--no-preflight")]
        public bool NoPreflight { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings s)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "workshop demo — 5 chapters, 12 steps", Theme.Neon);

        var state = DemoState.Load();
        var chapters = Chapters.All;

        if (!s.NoPreflight)
        {
            con.MarkupLine($"[{Theme.Steel}]running preflight…[/]");
            await new DoctorCommand().ExecuteAsync(context);
            con.WriteLine();
            if (!AnsiConsole.Confirm($"[{Theme.Pulse}]preflight done. start the demo?[/]", true))
                return 0;
        }

        // optional chapter / step jump
        var startCh = (s.FromChapter ?? 1) - 1;
        if (startCh < 0) startCh = 0;
        if (startCh >= chapters.Count) startCh = chapters.Count - 1;

        var runner = new StepRunner(con, state, s.ScriptedAll);

        for (var ci = startCh; ci < chapters.Count; ci++)
        {
            var ch = chapters[ci];
            ShowChapterBanner(con, ch);

            // Inside a chapter, the FromStep jump only applies to the first chapter
            var firstStep = ci == startCh ? (s.FromStep ?? ch.Steps.First().Number) : ch.Steps.First().Number;
            foreach (var step in ch.Steps)
            {
                if (step.Number < firstStep) continue;

                state.CurrentChapter = ch.Number;
                state.CurrentStep = step.Number;
                state.Save();

                var outcome = await runner.RunAsync(step, default);
                if (outcome == StepOutcome.Aborted) { con.MarkupLine($"[{Theme.Amber}]demo aborted by presenter.[/]"); return 0; }
            }

            ShowChapterFooter(con, ch);
            if (ci < chapters.Count - 1)
            {
                if (!AnsiConsole.Confirm($"[{Theme.Pulse}]continue to chapter {ci + 2} ([italic]{chapters[ci + 1].Title.EscapeMarkup()}[/])?[/]", true))
                {
                    con.MarkupLine($"[{Theme.Amber}]pausing demo — resume with `harness demo --from-chapter {ci + 2}`.[/]");
                    return 0;
                }
            }
        }

        ShowFinale(con);
        return 0;
    }

    private static void ShowChapterBanner(IAnsiConsole con, Chapter ch)
    {
        con.WriteLine();
        var rule = new Rule($"[{Theme.Neon}]chapter {ch.Number}/{Chapters.All.Count} · {ch.Title.EscapeMarkup()}[/]")
        {
            Style = Style.Parse(Theme.Neon),
            Justification = Justify.Left,
        };
        con.Write(rule);
        con.MarkupLine($"  [{Theme.Steel}]{ch.Subtitle.EscapeMarkup()}[/]");
        con.WriteLine();
    }

    private static void ShowChapterFooter(IAnsiConsole con, Chapter ch)
    {
        con.WriteLine();
        Theme.Rule(con, $"end of chapter {ch.Number} — {ch.Title.ToLowerInvariant()}", Theme.Acid);
    }

    private static void ShowFinale(IAnsiConsole con)
    {
        con.WriteLine();
        con.Write(new FigletText("THAT'S A WRAP").Color(Color.Chartreuse1).Centered());
        con.WriteLine();
        con.MarkupLine($"  [{Theme.Bone}]Custom harness · Microsoft identity, governance, observability.[/]");
        con.MarkupLine($"  [{Theme.Steel}]Leave-behind: workshop/leave-behind.md   |   Diagram: workshop/architecture.excalidraw   |   Slides: workshop/slides.pptx[/]");
        con.WriteLine();
    }
}
