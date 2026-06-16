namespace YourCustomAgentHarness.Tui.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using YourCustomAgentHarness.Tui.Demo;

/// <summary>
/// <c>harness lint-demo</c> — sanity-checks every markup-rendered string in
/// <see cref="Chapters.All"/> by constructing a Spectre <see cref="Markup"/>.
/// Catches `[Amber]`-style bugs (literal Theme alias used as color tag) and
/// unbalanced/invalid tags before the live workshop run.
/// </summary>
public sealed class LintDemoCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context)
    {
        var con = AnsiConsole.Console;
        Theme.RenderBanner(con);
        Theme.Rule(con, "lint-demo — validate Spectre markup across all chapter content", Theme.Neon);

        var errors = new List<(string where, string text, string error)>();
        var chapters = Chapters.All;
        var checks = 0;

        foreach (var ch in chapters)
        {
            TryMarkup($"Chapter {ch.Number}.Title", ch.Title, errors, ref checks);
            TryMarkup($"Chapter {ch.Number}.Subtitle", ch.Subtitle, errors, ref checks);
            foreach (var step in ch.Steps)
            {
                var prefix = $"Ch{ch.Number}/Step{step.Number}";
                TryMarkup($"{prefix}.Title", step.Title, errors, ref checks);
                TryMarkup($"{prefix}.Intro", step.Intro, errors, ref checks);
                for (var i = 0; i < step.BulletPoints.Length; i++)
                    TryMarkup($"{prefix}.BulletPoints[{i}]", step.BulletPoints[i], errors, ref checks);
                for (var i = 0; i < step.PortalLinks.Length; i++)
                {
                    TryMarkup($"{prefix}.PortalLinks[{i}].Title", step.PortalLinks[i].Title, errors, ref checks);
                    TryMarkup($"{prefix}.PortalLinks[{i}].Note", step.PortalLinks[i].Note, errors, ref checks);
                }
            }
        }

        con.MarkupLine($"[{Theme.Steel}]checked {checks} markup strings across {chapters.Count} chapters.[/]");

        if (errors.Count == 0)
        {
            con.MarkupLine($"[{Theme.Acid}]PASS[/]  all markup valid — demo is safe to run end-to-end.");
            return Task.FromResult(0);
        }

        con.MarkupLine($"[{Theme.Ember}]FAIL[/]  {errors.Count} bad markup string(s):");
        foreach (var (where, text, err) in errors)
        {
            con.MarkupLine($"  [{Theme.Amber}]{where.EscapeMarkup()}[/]");
            con.MarkupLine($"    [{Theme.Steel}]error:[/] {err.EscapeMarkup()}");
            con.MarkupLine($"    [{Theme.Steel}]text :[/] [grey50]{text.EscapeMarkup()}[/]");
        }
        return Task.FromResult(1);
    }

    private static void TryMarkup(string where, string text, List<(string, string, string)> errors, ref int checks)
    {
        if (string.IsNullOrEmpty(text)) return;
        checks++;
        try { _ = new Markup(text); }
        catch (Exception ex) { errors.Add((where, text, ex.Message)); }
    }
}
