namespace YourCustomAgentHarness.Api;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using YourCustomAgentHarness.Shared.Manifest;

/// <summary>
/// Discovers running agent instances by their blueprint hosting port and proxies
/// chat + identity requests. This lets the web UI talk to "the harness" rather than
/// directly to each agent process.
/// </summary>
public sealed class AgentRegistry
{
    private readonly BlueprintRegistry _bp;
    private readonly IHttpClientFactory _http;

    public AgentRegistry(BlueprintRegistry bp, IHttpClientFactory http)
    {
        _bp = bp;
        _http = http;
    }

    public async Task<object> SnapshotAsync()
    {
        var entries = _bp.All();
        var http = _http.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(2);
        var rows = new List<object>();
        foreach (var entry in entries)
        {
            var b = entry.Blueprint;
            var port = b.Hosting.ListenPort;
            var baseUrl = $"http://localhost:{port}";
            bool healthy = false; object? identity = null; string status = "offline";
            try
            {
                var h = await http.GetAsync($"{baseUrl}/api/health");
                if (h.IsSuccessStatusCode)
                {
                    healthy = true; status = "online";
                    var i = await http.GetAsync($"{baseUrl}/api/identity");
                    if (i.IsSuccessStatusCode)
                        identity = await JsonSerializer.DeserializeAsync<object>(await i.Content.ReadAsStreamAsync());
                }
            }
            catch
            {
                status = "offline";
            }

            var entryPoint = string.IsNullOrWhiteSpace(b.Hosting.EntryPoint)
                ? $"apps/{b.Metadata.Id}"
                : b.Hosting.EntryPoint;

            rows.Add(new
            {
                id = b.Metadata.Id,
                name = b.Metadata.Name,
                displayName = b.Identity.DisplayName,
                tagline = b.Identity.Tagline,
                description = b.Identity.Description,
                authMode = b.AuthMode,
                owner = b.Ownership.Owner,
                sponsor = b.Ownership.Sponsor,
                modelDeployment = b.Model.DeploymentName,
                modelEndpoint = b.Model.Endpoint,
                category = b.Identity.Category,
                blueprintFile = entry.FileName,
                blueprintId = b.Metadata.Id,
                endpoint = baseUrl,
                cliHint = $"dotnet run --project {entryPoint}",
                permissions = new
                {
                    delegated = b.Permissions.Delegated.SelectMany(p => p.Scopes).ToArray(),
                    application = b.Permissions.Application.SelectMany(p => p.Roles).ToArray()
                },
                mcpServers = b.McpServers.Select(s => new { s.Id, s.Scope, s.Endpoint }).ToArray(),
                hosting = new { baseUrl, port, status, healthy },
                status,
                identity
            });
        }
        return rows;
    }

    public async Task<IResult> ProxyChatAsync(string id, string jsonBody)
    {
        var bp = _bp.Get(id);
        if (bp is null) return Results.NotFound(new { error = "agent blueprint not found" });
        var baseUrl = $"http://localhost:{bp.Hosting.ListenPort}";
        var http = _http.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        try
        {
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync($"{baseUrl}/chat", content);
            var body = await resp.Content.ReadAsStringAsync();
            return Results.Content(body, "application/json", statusCode: (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            return Results.Json(new
            {
                reply = $"Agent '{id}' is not running on {baseUrl}. Start it from the TUI (`harness demo`) or `dotnet run` in apps/{id}.",
                blocked = false,
                direction = "agent-to-user",
                reason = ex.Message,
                citations = Array.Empty<object>()
            });
        }
    }
}
