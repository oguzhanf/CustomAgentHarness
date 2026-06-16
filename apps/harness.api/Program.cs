using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using YourCustomAgentHarness.Api;
using YourCustomAgentHarness.Shared.Manifest;
using YourCustomAgentHarness.Shared.Telemetry;
using AEv = YourCustomAgentHarness.Shared.Telemetry.ActivityEvent;

// Load .env (if present) so a single file can supply tenant/foundry/app-id settings.
YourCustomAgentHarness.Shared.DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient();

var blueprintsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "blueprints"));
builder.Services.AddSingleton(new BlueprintRegistry(blueprintsDir));
builder.Services.AddSingleton(new EventBus(capacity: 5000));
builder.Services.AddSingleton<AgentRegistry>();

var app = builder.Build();
app.UseCors();

// Tenant/subscription/foundry identifiers come from the user-provided tenant-state.yaml
// (falls back to the committed tenant-state.example.yaml on a fresh clone). Nothing tenant-
// specific is hard-coded; fill tenant-state.yaml to make the UI reflect YOUR environment.
var tenantState = TenantStateLoader.Load();

// ── Health
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", server = "harness.api", ts = DateTimeOffset.UtcNow }));

// ── Tenant + state (read-only summary for the UI status bar) — sourced from tenant-state.yaml
app.MapGet("/api/tenant", () => Results.Ok(new
{
    tenantId = tenantState.Tenant.Id,
    tenantDomain = tenantState.Tenant.Domain,
    tenantDisplayName = tenantState.Tenant.DisplayName,
    subscriptionId = tenantState.Subscription.Id,
    owner = tenantState.Tenant.Admin,
    harnessName = "YourCustomAgentHarness",
    bankName = "AgenticBank",
    foundry = new
    {
        accountName = tenantState.Foundry.AccountName,
        resourceGroup = tenantState.Foundry.ResourceGroup,
        region = tenantState.Foundry.Region,
        endpoint = tenantState.Foundry.Endpoint,
        deployments = tenantState.Foundry.Deployments
            .Select(d => new { name = d.Name, model = d.Name, boundTo = d.BoundTo })
            .ToArray()
    }
}));

// ── Blueprints catalog
app.MapGet("/api/blueprints", (BlueprintRegistry reg) => Results.Ok(reg.All().Select(e => new
{
    id = e.Blueprint.Metadata.Id,
    fileName = e.FileName,
    draft = e.IsDraft,
    apiVersion = e.Blueprint.ApiVersion,
    name = e.Blueprint.Metadata.Name,
    version = e.Blueprint.Metadata.Version,
    displayName = e.Blueprint.Identity.DisplayName,
    description = e.Blueprint.Identity.Description,
    tagline = e.Blueprint.Identity.Tagline,
    category = e.Blueprint.Identity.Category,
    authMode = e.Blueprint.AuthMode,
    owner = e.Blueprint.Ownership.Owner,
    sponsor = e.Blueprint.Ownership.Sponsor,
    modelDeployment = e.Blueprint.Model.DeploymentName,
    modelEndpoint = e.Blueprint.Model.Endpoint,
    listenPort = e.Blueprint.Hosting.ListenPort,
    permissions = new
    {
        delegated = e.Blueprint.Permissions.Delegated.SelectMany(p => p.Scopes).ToArray(),
        application = e.Blueprint.Permissions.Application.SelectMany(p => p.Roles).ToArray()
    }
})));

app.MapGet("/api/blueprints/{id}", (string id, BlueprintRegistry reg) =>
{
    var bp = reg.Get(id);
    return bp is null ? Results.NotFound() : Results.Ok(bp);
});

app.MapPost("/api/blueprints", async (HttpRequest req, BlueprintRegistry reg) =>
{
    using var sr = new StreamReader(req.Body);
    var raw = await sr.ReadToEndAsync();
    string yaml;
    string? fileName = null;

    var contentType = req.ContentType ?? string.Empty;
    var looksLikeJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                        || raw.TrimStart().StartsWith('{');

    if (looksLikeJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("yaml", out var y) && y.ValueKind == JsonValueKind.String)
                    yaml = y.GetString() ?? string.Empty;
                else
                {
                    return Results.BadRequest(new { error = "JSON body must include a `yaml` string property." });
                }
                if (doc.RootElement.TryGetProperty("fileName", out var f) && f.ValueKind == JsonValueKind.String)
                    fileName = f.GetString();
            }
            else { yaml = raw; }
        }
        catch (JsonException)
        {
            yaml = raw;
        }
    }
    else
    {
        yaml = raw;
    }

    try
    {
        var saved = reg.SaveDraft(yaml, fileName);
        ActivityStream.Instance.Emit(AEv.Create("harness.api", "blueprint", "blueprint.created", "ok",
            new Dictionary<string, object?>
            {
                ["id"] = saved.Blueprint.Metadata.Id,
                ["fileName"] = saved.FileName,
                ["draft"] = saved.IsDraft
            }));
        return Results.Ok(new
        {
            id = saved.Blueprint.Metadata.Id,
            fileName = saved.FileName,
            draft = saved.IsDraft
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// ── Agent instances (live runtime info from each agent's /api/health + /api/identity)
app.MapGet("/api/agents", async (AgentRegistry agents) => Results.Ok(await agents.SnapshotAsync()));

app.MapPost("/api/agents/{id}/chat", async (string id, HttpRequest req, AgentRegistry agents) =>
{
    using var sr = new StreamReader(req.Body);
    var body = await sr.ReadToEndAsync();
    return await agents.ProxyChatAsync(id, body);
});

// ── Portal deep-links (the TUI + UI use these to drive the browser)
app.MapGet("/api/portals", () => Results.Ok(new[]
{
    new {
        id = "admin",
        title = "Microsoft 365 admin center — Agents",
        description = "See the agent registered against this tenant, with permissions, owner and lifecycle status.",
        url = "https://admin.microsoft.com/Adminportal/Home#/agents",
        icon = "shield",
        category = "admin"
    },
    new {
        id = "entra-blueprints",
        title = "Entra ID — Agent ID Blueprints",
        description = "The blueprint object that defines the agent's identity, ownership and scope template.",
        url = "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/AgentIdBlueprintMenuBlade",
        icon = "fingerprint",
        category = "identity"
    },
    new {
        id = "entra-agents",
        title = "Entra ID — Agent identities",
        description = "Each minted agent identity, its credentials, sign-ins and risk assessments.",
        url = "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/AgentIdMenuBlade",
        icon = "id-card",
        category = "identity"
    },
    new {
        id = "purview",
        title = "Purview — DSPM for AI",
        description = "DLP and Insider Risk policies that govern what content agents may see or emit.",
        url = "https://purview.microsoft.com/datasecurity/dspmforai",
        icon = "lock",
        category = "purview"
    },
    new {
        id = "foundry",
        title = $"Azure AI Foundry — {tenantState.Foundry.AccountName}",
        description = "Model deployments backing the harness — content filters, capacity and metrics.",
        url = $"https://ai.azure.com/?wsid=/subscriptions/{tenantState.Subscription.Id}/resourceGroups/{tenantState.Foundry.ResourceGroup}/providers/Microsoft.CognitiveServices/accounts/{tenantState.Foundry.AccountName}",
        icon = "cpu",
        category = "foundry"
    },
    new {
        id = "defender",
        title = "Defender for Cloud Apps — Agents",
        description = "Anomalous-behavior detection and runtime telemetry for agent identities.",
        url = "https://security.microsoft.com/agents",
        icon = "radar",
        category = "defender"
    }
}));

// ── Event ingest from agents/TUI
app.MapPost("/api/_ingest", async (HttpRequest req, EventBus bus) =>
{
    try
    {
        var ev = await JsonSerializer.DeserializeAsync<AEv>(req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (ev is null || string.IsNullOrWhiteSpace(ev.Source) || string.IsNullOrWhiteSpace(ev.Kind))
            return Results.BadRequest(new { error = "event requires `source` and `kind` (camelCase)" });
        bus.Publish(ev);
        return Results.Ok();
    }
    catch (JsonException jx)
    {
        return Results.BadRequest(new { error = "invalid JSON: " + jx.Message });
    }
});

// ── SSE stream of all events
app.MapGet("/api/events", async (HttpContext ctx, EventBus bus, CancellationToken ct) =>
{
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("X-Accel-Buffering", "no");

    var seen = new List<AEv>(bus.Snapshot());
    foreach (var ev in seen.TakeLast(200))
    {
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(ev)}\n\n", ct);
    }
    await ctx.Response.Body.FlushAsync(ct);

    using var sub = bus.Subscribe(ct);
    await foreach (var ev in sub.Reader.ReadAllAsync(ct))
    {
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(ev)}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
});

// Also stream local events
ActivityStream.Instance.OnEvent += ev =>
{
    var bus = app.Services.GetRequiredService<EventBus>();
    bus.Publish(ev);
};

app.Run();
