namespace YourCustomAgentHarness.ForgedAgentOne;

using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.SemanticKernel;
using YourCustomAgentHarness.Shared.Telemetry;

/// <summary>
/// Semantic Kernel plugin exposing OBO-flavoured Microsoft Graph tools.
/// Tokens are passed via Kernel.Data ["UserAccessToken"] — populated per request.
/// In production, the token would come from the bearer token presented to the
/// agent's /api/messages or /chat endpoint and exchanged via the OBO flow.
/// For demo purposes we either use the raw delegated token from MSAL.js
/// (if present) or fall back to canned data for safe demo continuity.
/// </summary>
public sealed class GraphPlugin
{
    private readonly IHttpClientFactory _httpFactory;
    public GraphPlugin(IHttpClientFactory httpFactory) => _httpFactory = httpFactory;

    [KernelFunction("list_recent_mail")]
    [Description("Lists the signed-in banker's most recent inbox messages (subject + from + preview). Use when the user asks to summarize, triage or check their mail.")]
    public async Task<string> ListRecentMailAsync(
        Kernel kernel,
        [Description("Number of messages to fetch (default 5, max 10).")] int top = 5,
        CancellationToken ct = default)
    {
        top = Math.Clamp(top, 1, 10);
        var token = kernel.Data["UserAccessToken"] as string;
        var upn = kernel.Data["UserUpn"] as string ?? "unknown";
        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "ForgedAgentOne", "obo", "graph.mail.list", "ok",
            new Dictionary<string, object?> { ["count"] = top, ["user"] = upn }));

        if (string.IsNullOrEmpty(token)) return Cannedmail();

        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var url = $"https://graph.microsoft.com/v1.0/me/messages?$top={top}&$select=subject,from,bodyPreview,receivedDateTime";
            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return $"(Graph returned {resp.StatusCode}. Demonstrating fallback canned data instead.)\n\n" + Cannedmail();
            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var items = doc.RootElement.GetProperty("value").EnumerateArray().Take(top).Select(m =>
                $"- **{m.GetProperty("subject").GetString()}** — {m.GetProperty("from").GetProperty("emailAddress").GetProperty("name").GetString()}: {Trim(m.GetProperty("bodyPreview").GetString(), 120)}");
            return string.Join('\n', items);
        }
        catch (Exception ex)
        {
            return $"(Graph call failed: {ex.GetType().Name}. Falling back to canned demo data.)\n\n" + Cannedmail();
        }
    }

    [KernelFunction("list_today_meetings")]
    [Description("Lists the signed-in banker's meetings for the current day. Use when the user asks about today's schedule.")]
    public async Task<string> ListTodayMeetingsAsync(Kernel kernel, CancellationToken ct = default)
    {
        var token = kernel.Data["UserAccessToken"] as string;
        var upn = kernel.Data["UserUpn"] as string ?? "unknown";
        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "ForgedAgentOne", "obo", "graph.calendar.today", "ok",
            new Dictionary<string, object?> { ["user"] = upn }));

        if (string.IsNullOrEmpty(token)) return CannedCalendar();

        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var start = DateTime.UtcNow.Date.ToString("o");
            var end = DateTime.UtcNow.Date.AddDays(1).ToString("o");
            var url = $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={start}&endDateTime={end}&$select=subject,start,end,attendees";
            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return $"(Graph returned {resp.StatusCode}.)\n\n" + CannedCalendar();
            var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var items = doc.RootElement.GetProperty("value").EnumerateArray().Select(m =>
                $"- **{m.GetProperty("subject").GetString()}** ({m.GetProperty("start").GetProperty("dateTime").GetString()})");
            var arr = items.ToList();
            return arr.Count == 0 ? "(No meetings on the calendar today.)" : string.Join('\n', arr);
        }
        catch (Exception ex)
        {
            return $"(Graph call failed: {ex.GetType().Name}.)\n\n" + CannedCalendar();
        }
    }

    [KernelFunction("search_sharepoint")]
    [Description("Searches the signed-in banker's accessible SharePoint sites for documents matching a query. Use when the user asks to find a policy, contract, or shared file.")]
    public Task<string> SearchSharePointAsync(Kernel kernel, [Description("Free-text search query")] string query, CancellationToken ct = default)
    {
        var upn = kernel.Data["UserUpn"] as string ?? "unknown";
        ActivityStream.Instance.Emit(ActivityEvent.Create(
            "ForgedAgentOne", "obo", "graph.sharepoint.search", "ok",
            new Dictionary<string, object?> { ["user"] = upn, ["query"] = query }));

        // For brevity we use canned content here. Replace with /search/query POST for live Graph.
        return Task.FromResult(CannedSharePoint(query));
    }

    private static string Trim(string? s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

    private static string Cannedmail() => """
- **Q3 portfolio review — Pawel Nowak** — Pawel Nowak: Could we sit down Thursday to walk through the new exposures on the Polish industrials book?
- **AML refresher training due** — Learning & Development: Quick reminder — your annual AML certification expires in 7 days.
- **Customer onboarding — Acme Logistics** — KYC Operations: We have the UBO declaration and proof-of-address. Pending: source-of-funds confirmation > EUR 5M.
- **Friday team coffee** — Anya Verkhovska: Friday 10:00 — usual place. Bring your roadmap thoughts.
- **Sanctions alert TM-027** — Compliance Auto: An incoming wire to Acme Logistics flagged on the sanctions list. Hold placed.
""";

    private static string CannedCalendar() => """
- **09:30 — Daily standup with credit risk** (online)
- **11:00 — Customer call: Acme Logistics renewal** (Teams)
- **13:30 — Lunch with regional head** (cafeteria)
- **15:00 — Quarterly compliance review** (Boardroom A)
- **16:30 — AI Centre of Excellence weekly** (online)
""";

    private static string CannedSharePoint(string q) => $"""
Top results for "{q}":
- **AgenticBank — KYC Policy v4.2 (PSL-001)** — Compliance / Policies / Onboarding — last modified 2025-09-01
- **AgenticBank — High-Risk Customer Procedure (PSL-006)** — Compliance / Procedures — last modified 2026-02-20
- **AgenticBank — AI Use Policy (PSL-007)** — Tech Risk / Policies — last modified 2026-03-01
""";
}
