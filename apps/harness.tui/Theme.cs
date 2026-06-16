namespace YourCustomAgentHarness.Tui;

using Spectre.Console;
using Spectre.Console.Rendering;

/// <summary>
/// CustomAgentHarness cyberpunk theme tokens. Use <see cref="Style"/> values via
/// <c>"[neon]text[/]"</c> markup. Resolved at startup against the configured
/// AnsiConsole color palette.
/// </summary>
public static class Theme
{
    // ── colour tokens (24-bit; Spectre maps to nearest available in the terminal) ──
    public const string Neon = "darkorange3_1";       // primary CTA
    public const string Pulse = "mediumpurple1";       // secondary accent
    public const string Cyan = "deepskyblue1";         // data / hyperlinks
    public const string Steel = "grey78";              // dim labels
    public const string Bone = "grey93";               // body text
    public const string Acid = "chartreuse1";          // success
    public const string Ember = "red1";                // failure / block
    public const string Amber = "gold1";               // warning / scripted

    /// <summary>The harness brand banner. Cheap drama at startup only.</summary>
    public static void RenderBanner(IAnsiConsole con)
    {
        var fig = new FigletText("CustomAgentHarness")
            .LeftJustified()
            .Color(Color.DarkOrange3_1);
        con.Write(fig);
        con.MarkupLine($"  [{Steel}]agent-365 integration plane · {DateTime.Now:yyyy-MM-dd HH:mm}[/]");
        con.WriteLine();
    }

    public static string Tag(string text, string colorToken) => $"[{colorToken}]{text.EscapeMarkup()}[/]";

    /// <summary>Print a thin separator rule with a dim caption.</summary>
    public static void Rule(IAnsiConsole con, string caption, string color = Pulse)
    {
        var rule = new Rule($"[{color}]{caption.EscapeMarkup()}[/]")
        {
            Style = Style.Parse(Steel),
            Justification = Justify.Left
        };
        con.Write(rule);
    }

    public static Panel Panel(string title, IRenderable body, string accent = Pulse) =>
        new Panel(body)
            .Header($"[{accent}]{title.EscapeMarkup()}[/]", Justify.Left)
            .Border(BoxBorder.Rounded)
            .BorderColor(accent switch
            {
                Neon => Color.DarkOrange3_1,
                Pulse => Color.MediumPurple1,
                Cyan => Color.DeepSkyBlue1,
                Acid => Color.Chartreuse1,
                Ember => Color.Red1,
                Amber => Color.Gold1,
                _ => Color.Grey78
            })
            .Padding(1, 0, 1, 0);
}
