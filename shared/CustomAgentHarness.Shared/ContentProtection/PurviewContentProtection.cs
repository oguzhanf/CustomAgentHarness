namespace YourCustomAgentHarness.Shared.ContentProtection;

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YourCustomAgentHarness.Shared.Telemetry;
using AEv = YourCustomAgentHarness.Shared.Telemetry.ActivityEvent;

/// <summary>
/// Real Microsoft Purview content protection. Calls Microsoft Graph
/// <c>/users/{id}/dataSecurityAndGovernance/processContent</c> for every
/// inbound prompt and outbound response. Translates the response into the
/// harness-native <see cref="ClassificationResult"/> so it is a drop-in
/// replacement for the regex-only classifier.
/// </summary>
/// <remarks>
/// Requires the calling identity (TokenCredential) to hold one of:
/// <c>Content.Process.User</c> (delegated) or <c>Content.Process.All</c> (app),
/// plus <c>ProtectionScopes.Compute.User</c>/<c>.All</c>.
/// In the workshop we use <see cref="Azure.Identity.AzureCliCredential"/>
/// (the same identity that az is already signed in as) so device-login flow
/// is preserved end-to-end and customers see real Graph traffic.
/// </remarks>
public sealed class PurviewContentProtection : IContentProtection
{
    private static readonly string[] GraphScopes = ["https://graph.microsoft.com/.default"];

    private readonly HttpClient _http;
    private readonly TokenCredential _credential;
    private readonly PurviewOptions _options;
    private readonly ContentClassifier _fallback;
    private readonly ILogger<PurviewContentProtection> _logger;

    public PurviewContentProtection(
        HttpClient http,
        TokenCredential credential,
        PurviewOptions options,
        ContentClassifier fallback,
        ILogger<PurviewContentProtection>? logger = null)
    {
        _http = http;
        _credential = credential;
        _options = options;
        _fallback = fallback;
        _logger = logger ?? NullLogger<PurviewContentProtection>.Instance;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string text,
        string direction,
        string? userObjectId = null,
        string? conversationId = null,
        int sequenceNumber = 0,
        CancellationToken ct = default)
    {
        var mode = (_options.Mode ?? "Auto").Trim();

        // If Purview can't possibly succeed (no AppLocationValue → no scope ever returned),
        // degrade Auto to Fallback so the regex SIT classifier still protects us. Real mode
        // keeps surfacing the config error.
        if (string.IsNullOrWhiteSpace(_options.AppLocationValue) &&
            string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            var fb = _fallback.Classify(text, direction);
            return fb with { ClassifierUsed = "Regex fallback (Purview AppLocationValue not configured — run `a365 setup blueprint`)" };
        }

        if (string.Equals(mode, "Fallback", StringComparison.OrdinalIgnoreCase))
        {
            return _fallback.Classify(text, direction);
        }

        var effectiveUserId = userObjectId ?? _options.DefaultUserObjectId;
        if (string.IsNullOrWhiteSpace(effectiveUserId))
        {
            // Graph processContent requires a user id in the URL. Without one,
            // in Auto we degrade to the regex fallback; in Real we surface an error verdict.
            if (string.Equals(mode, "Real", StringComparison.OrdinalIgnoreCase))
            {
                return new ClassificationResult(
                    Blocked: true,
                    Direction: direction,
                    Hits: Array.Empty<SensitiveHit>(),
                    ClassifierUsed: "Purview (config error: no user oid)",
                    ClassifiedAt: DateTimeOffset.UtcNow);
            }
            var fb = _fallback.Classify(text, direction);
            return fb with { ClassifierUsed = "Regex fallback (no user oid for Purview)" };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var verdict = await CallProcessContentAsync(text, direction, effectiveUserId, conversationId, sequenceNumber, ct).ConfigureAwait(false);
            sw.Stop();

            ActivityStream.Instance.Emit(AEv.Create(
                _options.AppName, "purview", $"graph.processContent.{direction}",
                verdict.RestrictAccess ? "block" : "ok",
                new Dictionary<string, object?>
                {
                    ["userOid"]         = effectiveUserId,
                    ["restrictAction"]  = verdict.RestrictionAction,
                    ["scopeState"]      = verdict.ProtectionScopeState,
                    ["actionTypes"]     = string.Join(",", verdict.RawActionTypes),
                    ["processingErrors"]= verdict.ProcessingErrors.Count,
                    ["activity"]        = direction == "user-to-agent" ? "uploadText" : "downloadText"
                },
                sw.Elapsed.TotalMilliseconds));

            if (verdict.RestrictAccess)
            {
                return new ClassificationResult(
                    Blocked: true,
                    Direction: direction,
                    Hits: Array.Empty<SensitiveHit>(),
                    ClassifierUsed: $"Microsoft Purview (Graph processContent → {verdict.RestrictionAction ?? "restrictAccess"})",
                    ClassifiedAt: DateTimeOffset.UtcNow);
            }

            return new ClassificationResult(
                Blocked: false,
                Direction: direction,
                Hits: Array.Empty<SensitiveHit>(),
                ClassifierUsed: "Microsoft Purview (Graph processContent → allow)",
                ClassifiedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Purview Graph call failed in {Direction}; mode={Mode}", direction, mode);
            ActivityStream.Instance.Emit(AEv.Create(
                _options.AppName, "purview", $"graph.processContent.{direction}", "error",
                new Dictionary<string, object?>
                {
                    ["error"] = ex.GetType().Name,
                    ["msg"]   = ex.Message,
                    ["mode"]  = mode
                },
                sw.Elapsed.TotalMilliseconds));

            if (string.Equals(mode, "Real", StringComparison.OrdinalIgnoreCase))
            {
                return new ClassificationResult(
                    Blocked: true,
                    Direction: direction,
                    Hits: Array.Empty<SensitiveHit>(),
                    ClassifierUsed: $"Microsoft Purview (error: {ex.GetType().Name})",
                    ClassifiedAt: DateTimeOffset.UtcNow);
            }
            // Auto → fall back to regex
            var fb = _fallback.Classify(text, direction);
            return fb with { ClassifierUsed = $"Regex fallback (Purview unavailable: {ex.GetType().Name})" };
        }
    }

    private async Task<PurviewProcessContentVerdict> CallProcessContentAsync(
        string text,
        string direction,
        string userObjectId,
        string? conversationId,
        int sequenceNumber,
        CancellationToken ct)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(GraphScopes), ct).ConfigureAwait(false);

        var activity = direction == "user-to-agent" ? "uploadText" : "downloadText";
        var body = PurviewProcessContentEnvelope.Build(
            text: text,
            activity: activity,
            contentEntryId: Guid.NewGuid().ToString(),
            conversationCorrelationId: conversationId,
            sequenceNumber: sequenceNumber,
            appName: _options.AppName,
            appVersion: _options.AppVersion,
            appLocationType: _options.AppLocationType,
            appLocationValue: _options.AppLocationValue);

        var baseUri = _options.GraphBaseUri.TrimEnd('/');
        var url = $"{baseUri}/users/{userObjectId}/dataSecurityAndGovernance/processContent";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        req.Headers.UserAgent.ParseAdd("CustomAgentHarness/1.0");

        using var timed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timed.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, _options.RequestTimeoutSeconds)));

        var resp = await _http.SendAsync(req, timed.Token).ConfigureAwait(false);
        var content = await resp.Content.ReadAsStringAsync(timed.Token).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Purview processContent returned {(int)resp.StatusCode} {resp.StatusCode}: {Truncate(content, 500)}");
        }

        return PurviewProcessContentVerdict.Parse(content);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max) + "…");
}
