extern alias AzIdentity;
using DefaultAzureCredential = AzIdentity::Azure.Identity.DefaultAzureCredential;
using DefaultAzureCredentialOptions = AzIdentity::Azure.Identity.DefaultAzureCredentialOptions;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using YourCustomAgentHarness.ForgedScholarTwo;
using YourCustomAgentHarness.Shared.ContentProtection;
using YourCustomAgentHarness.Shared.Manifest;
using YourCustomAgentHarness.Shared.Telemetry;

// Load .env (if present) so a single file can supply tenant/model/Purview settings.
YourCustomAgentHarness.Shared.DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

var blueprintPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "blueprints", "forged-scholar-two.harness.yaml"));
var blueprint = BlueprintLoader.LoadYaml(blueprintPath);
builder.Services.AddSingleton(blueprint);
builder.Services.AddHarnessContentProtection(builder.Configuration, blueprint);

const string AgentName = "ForgedScholarTwo";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(AgentName, serviceVersion: "1.0.0"))
    .WithTracing(t => t.AddSource(AgentName).AddConsoleExporter())
    .WithMetrics(m => m.AddMeter(AgentName).AddConsoleExporter());

var aoaiKey = builder.Configuration["AzureOpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// Foundry endpoint: FOUNDRY_ENDPOINT env (e.g. from .env) wins, else the blueprint value.
var aoaiEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT") ?? blueprint.Model.Endpoint;

var skKernel = builder.Services.AddKernel();
if (!string.IsNullOrWhiteSpace(aoaiKey))
{
    skKernel.AddAzureOpenAIChatCompletion(
        deploymentName: blueprint.Model.DeploymentName,
        endpoint: aoaiEndpoint,
        apiKey: aoaiKey);
}
else
{
    var cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = false,
    });
    skKernel.AddAzureOpenAIChatCompletion(
        deploymentName: blueprint.Model.DeploymentName,
        endpoint: aoaiEndpoint,
        credentials: cred);
    builder.Services.AddSingleton<Azure.Core.TokenCredential>(cred);
}

builder.Services.AddHttpClient("kb", c =>
{
    var kbEndpoint = blueprint.McpServers.FirstOrDefault()?.Endpoint ?? "http://localhost:3981";
    c.BaseAddress = new Uri(kbEndpoint);
    c.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<ForgedScholarTwoService>();
builder.Services.AddHostedService<ActivityForwarder>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    agent = AgentName,
    blueprint = blueprint.Metadata.Id,
    model = blueprint.Model.DeploymentName,
    authMode = blueprint.AuthMode,
    mcpServers = blueprint.McpServers.Select(s => new { s.Id, s.Endpoint }).ToArray(),
    ts = DateTimeOffset.UtcNow
}));

app.MapGet("/api/identity", () => Results.Ok(new
{
    displayName = blueprint.Identity.DisplayName,
    description = blueprint.Identity.Description,
    tagline = blueprint.Identity.Tagline,
    owner = blueprint.Ownership.Owner,
    sponsor = blueprint.Ownership.Sponsor,
    authMode = blueprint.AuthMode,
    permissions = new
    {
        delegated = blueprint.Permissions.Delegated.SelectMany(p => p.Scopes).ToArray(),
        application = blueprint.Permissions.Application.SelectMany(p => p.Roles).ToArray(),
    },
    model = new { blueprint.Model.Endpoint, blueprint.Model.DeploymentName },
    mcpServers = blueprint.McpServers.Select(s => new { s.Id, s.Scope, s.Endpoint }).ToArray()
}));

app.MapPost("/chat", async (ChatRequest req, ForgedScholarTwoService svc, IContentProtection classifier, CancellationToken ct) =>
{
    var conv = req.ConversationId ?? Guid.NewGuid().ToString();

    var inbound = await classifier.ClassifyAsync(req.Message ?? "", "user-to-agent", req.UserObjectId, conv, sequenceNumber: 0, ct);
    ActivityStream.Instance.Emit(ActivityEvent.Create(
        AgentName, "block", "purview.inbound", inbound.Blocked ? "block" : "ok",
        new Dictionary<string, object?> { ["reason"] = inbound.Reason, ["classifier"] = inbound.ClassifierUsed, ["hits"] = inbound.Hits.Count }));
    if (inbound.Blocked)
    {
        var sitDetail = inbound.Hits.Count > 0
            ? " (" + string.Join(", ", inbound.Hits.Select(h => h.SitName).Distinct()) + ")"
            : "";
        return Results.Ok(new ChatResponse(
            "Your message was blocked by your organization's data-protection policy" + sitDetail + ".",
            Blocked: true, Direction: "user-to-agent", Reason: inbound.Reason, Citations: Array.Empty<Citation>()));
    }

    var reply = await svc.ChatAsync(req, ct);

    var outbound = await classifier.ClassifyAsync(reply.Text, "agent-to-user", req.UserObjectId, conv, sequenceNumber: 1, ct);
    ActivityStream.Instance.Emit(ActivityEvent.Create(
        AgentName, "block", "purview.outbound", outbound.Blocked ? "block" : "ok",
        new Dictionary<string, object?> { ["reason"] = outbound.Reason, ["classifier"] = outbound.ClassifierUsed, ["hits"] = outbound.Hits.Count }));
    if (outbound.Blocked)
    {
        var sitDetail = outbound.Hits.Count > 0
            ? " (" + string.Join(", ", outbound.Hits.Select(h => h.SitName).Distinct()) + ")"
            : "";
        return Results.Ok(new ChatResponse(
            "[Response withheld] The agent attempted to return sensitive content" + sitDetail
            + ". Purview blocked outbound delivery.",
            Blocked: true, Direction: "agent-to-user", Reason: outbound.Reason, Citations: Array.Empty<Citation>()));
    }

    return Results.Ok(new ChatResponse(reply.Text, false, "ok", "Clear", reply.Citations));
});

app.Run();

public sealed record ChatRequest(string? Message, string? UserUpn, string? UserObjectId, string? ConversationId);
public sealed record Citation(string DocumentId, string Title, string Source, double Score);
public sealed record ChatResponse(string Reply, bool Blocked, string Direction, string Reason, IReadOnlyList<Citation> Citations);
