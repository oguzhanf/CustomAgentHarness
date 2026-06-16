extern alias AzIdentity;
using DefaultAzureCredential = AzIdentity::Azure.Identity.DefaultAzureCredential;
using DefaultAzureCredentialOptions = AzIdentity::Azure.Identity.DefaultAzureCredentialOptions;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using YourCustomAgentHarness.ForgedAgentOne;
using YourCustomAgentHarness.Shared.ContentProtection;
using YourCustomAgentHarness.Shared.Manifest;
using YourCustomAgentHarness.Shared.Telemetry;

// Load .env (if present) so a single file can supply tenant/model/Purview settings.
YourCustomAgentHarness.Shared.DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// ---- Load the blueprint that describes THIS agent ----
var blueprintPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "blueprints", "forged-agent-one.harness.yaml");
blueprintPath = Path.GetFullPath(blueprintPath);
var blueprint = BlueprintLoader.LoadYaml(blueprintPath);

builder.Services.AddSingleton(blueprint);
builder.Services.AddHarnessContentProtection(builder.Configuration, blueprint);

// ---- OpenTelemetry: Console + (optional) OTLP to harness.api SSE bridge ----
const string AgentName = "ForgedAgentOne";
const string AgentVersion = "1.0.0";

var resource = ResourceBuilder.CreateDefault()
    .AddService(serviceName: AgentName, serviceVersion: AgentVersion)
    .AddAttributes(new KeyValuePair<string, object>[]
    {
        new("a365.agent.id", "forged-agent-one"),
        new("a365.harness", "YourCustomAgentHarness"),
        new("a365.tenant", builder.Configuration["TenantId"] ?? Environment.GetEnvironmentVariable("TENANT_ID") ?? "unknown"),
        new("a365.owner", blueprint.Ownership.Owner),
        new("a365.authMode", blueprint.AuthMode),
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(_ => _.AddService(AgentName, serviceVersion: AgentVersion))
    .WithTracing(t => t.AddSource(AgentName).AddConsoleExporter())
    .WithMetrics(m => m.AddMeter(AgentName).AddConsoleExporter());

// ---- Semantic Kernel + Azure OpenAI (Entra-auth; falls back to API key if present) ----
var aoaiKey = builder.Configuration["AzureOpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// Foundry endpoint: FOUNDRY_ENDPOINT env (e.g. from .env) wins, else the blueprint value.
var aoaiEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT") ?? blueprint.Model.Endpoint;

var kernelBuilder = builder.Services.AddKernel();
if (!string.IsNullOrWhiteSpace(aoaiKey))
{
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: blueprint.Model.DeploymentName,
        endpoint: aoaiEndpoint,
        apiKey: aoaiKey);
}
else
{
    // No key configured — use Entra-based auth (Foundry default; disableLocalAuth = true).
    // DefaultAzureCredential picks up AzureCliCredential / VS / env / managed identity.
    var cred = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeInteractiveBrowserCredential = false,
    });
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: blueprint.Model.DeploymentName,
        endpoint: aoaiEndpoint,
        credentials: cred);
    builder.Services.AddSingleton<Azure.Core.TokenCredential>(cred);
}

builder.Services.AddSingleton<ForgedAgentOneService>();

// ---- HTTP pipeline ----
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<ActivityForwarder>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    agent = AgentName,
    blueprint = blueprint.Metadata.Id,
    model = blueprint.Model.DeploymentName,
    authMode = blueprint.AuthMode,
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
    mcpServers = blueprint.McpServers.Select(s => new { s.Id, s.Scope }).ToArray()
}));

app.MapPost("/chat", async (ChatRequest req, ForgedAgentOneService svc, IContentProtection classifier, CancellationToken ct) =>
{
    var conv = req.ConversationId ?? Guid.NewGuid().ToString();

    // 1. Inbound classify (Purview uploadText if configured, regex fallback otherwise)
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
            "Your message was blocked by your organization's data-protection policy" + sitDetail
            + ". Please rephrase without account numbers, IBANs, card numbers, etc.",
            Blocked: true, Direction: "user-to-agent", Reason: inbound.Reason));
    }

    // 2. LLM call
    var reply = await svc.ChatAsync(req, ct);

    // 3. Outbound classify (Purview downloadText)
    var outbound = await classifier.ClassifyAsync(reply, "agent-to-user", req.UserObjectId, conv, sequenceNumber: 1, ct);
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
            Blocked: true, Direction: "agent-to-user", Reason: outbound.Reason));
    }

    return Results.Ok(new ChatResponse(reply, false, "ok", "Clear"));
});

app.Run();

public sealed record ChatRequest(string? Message, string? UserUpn, string? UserObjectId, string? UserAccessToken, string? ConversationId);
public sealed record ChatResponse(string Reply, bool Blocked, string Direction, string Reason);
