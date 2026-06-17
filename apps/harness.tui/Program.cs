using Spectre.Console;
using Spectre.Console.Cli;
using YourCustomAgentHarness.Tui.Commands;

// Load .env (if present) so child services the TUI spawns inherit the same settings.
YourCustomAgentHarness.Shared.DotEnv.Load();

var app = new CommandApp();
app.Configure(c =>
{
    c.SetApplicationName("harness");
    c.SetApplicationVersion("1.0.0");
    c.AddCommand<SetupCommand>("setup")
        .WithDescription("One-stop guided setup: prerequisites, .env config, Entra roles, provisioning, Purview DLP.");
    c.AddCommand<UpCommand>("up")
        .WithDescription("Start every service the harness owns (api, web, kb-mcp, both agents).");
    c.AddCommand<DownCommand>("down")
        .WithDescription("Stop every harness-managed process listed in state/processes.json.");
    c.AddCommand<StatusCommand>("status")
        .WithDescription("Show ports, PIDs and health for every harness-owned service.");
    c.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Run preflight checks (az login, a365 cli, Foundry reachability, Purview readiness).");
    c.AddCommand<DemoCommand>("demo")
        .WithDescription("Run the workshop demo (5 chapters, 12 steps, per-step fallback menu).");
    c.AddCommand<LintDemoCommand>("lint-demo")
        .WithDescription("Validate every Spectre markup string in Chapters.All — run before the live demo.");
});

if (args.Length == 0)
{
    AnsiConsole.Write(new FigletText("CustomAgentHarness").Color(Color.DarkOrange3_1));
    AnsiConsole.MarkupLine("  [grey78]agent-365 integration plane[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("  Commands:");
    AnsiConsole.MarkupLine("    [bold]harness setup[/]    — one-stop: prerequisites, .env, roles, provisioning, Purview");
    AnsiConsole.MarkupLine("    [bold]harness up[/]       — start every service (api, web, kb-mcp, agents)");
    AnsiConsole.MarkupLine("    [bold]harness down[/]     — stop every harness-managed service");
    AnsiConsole.MarkupLine("    [bold]harness status[/]   — port + process table for every service");
    AnsiConsole.MarkupLine("    [bold]harness doctor[/]   — preflight checks (az, a365, Foundry, Purview)");
    AnsiConsole.MarkupLine("    [bold]harness demo[/]     — drive the 5-chapter workshop walkthrough");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("  New here? Run [yellow]harness setup[/] first, then [yellow]harness up[/].");
    return 0;
}

return await app.RunAsync(args);

