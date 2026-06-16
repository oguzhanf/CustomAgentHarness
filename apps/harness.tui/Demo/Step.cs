namespace YourCustomAgentHarness.Tui.Demo;

using YourCustomAgentHarness.Tui.Runtime;

public enum StepMode { Live, Scripted }

public sealed record PortalLink(string Title, string Url, string Note = "");

/// <summary>
/// One demo step. Pure data — no execution logic — so the content is easy to
/// edit between rehearsals. <see cref="StepRunner"/> owns the actual rendering
/// and process-spawning.
/// </summary>
public sealed class Step
{
    public required int Number { get; init; }
    public required int Chapter { get; init; }
    public required string ChapterTitle { get; init; }
    public required string Title { get; init; }

    /// <summary>Narrative shown before the command. Markup is enabled.</summary>
    public required string Intro { get; init; }

    /// <summary>What we'll show on screen as "the command we're about to run".</summary>
    public string? LiveCommand { get; init; }

    /// <summary>Optional executable to invoke. Null = nothing to run, talk-only step.</summary>
    public string? Executable { get; init; }
    public string[] Args { get; init; } = Array.Empty<string>();
    public string? WorkingDirectory { get; init; }

    /// <summary>Soft timeout. Defaults to 15s per rubber-duck guidance.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Pre-recorded output to display when running in scripted mode (or on
    /// fallback). Should look believable — typical command output for the step.
    /// </summary>
    public string ScriptedOutput { get; init; } = "";

    /// <summary>Talking points the presenter walks through after the command runs.</summary>
    public string[] BulletPoints { get; init; } = Array.Empty<string>();

    /// <summary>Portal links to offer as a numbered menu after the step.</summary>
    public PortalLink[] PortalLinks { get; init; } = Array.Empty<PortalLink>();

    /// <summary>If the step's preconditions are uncertain, default to scripted.</summary>
    public StepMode DefaultMode { get; init; } = StepMode.Scripted;

    /// <summary>
    /// Optional hook to update <see cref="DemoState"/> after a successful live run
    /// (e.g. parse an AppId out of CLI output and stash it).
    /// </summary>
    public Action<DemoState, string>? OnSuccess { get; init; }

    /// <summary>
    /// If true, the runner will not prompt the audience for input between this
    /// step's panels — useful for cosmetic / talk-only steps.
    /// </summary>
    public bool AutoAdvance { get; init; }
}

public sealed class Chapter
{
    public required int Number { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required IReadOnlyList<Step> Steps { get; init; }
}
