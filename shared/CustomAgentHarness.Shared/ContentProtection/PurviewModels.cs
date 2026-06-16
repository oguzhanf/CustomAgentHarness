namespace YourCustomAgentHarness.Shared.ContentProtection;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// <summary>
/// Minimal request/response shapes for the Microsoft Graph
/// <c>POST /users/{id}/dataSecurityAndGovernance/processContent</c> endpoint.
/// We hand-roll JSON via <see cref="JsonNode"/> because the polymorphic
/// <c>@odata.type</c> discriminator is awkward with System.Text.Json source-gen.
/// </summary>
internal static class PurviewProcessContentEnvelope
{
    /// <summary>
    /// Build the full <c>processContent</c> request body for a single text payload.
    /// </summary>
    public static JsonObject Build(
        string text,
        string activity,             // uploadText | downloadText
        string contentEntryId,
        string? conversationCorrelationId,
        int sequenceNumber,
        string appName,
        string appVersion,
        string appLocationType,      // Application | Uri | Domain
        string appLocationValue)
    {
        var locationOdataType = appLocationType.Equals("Uri", StringComparison.OrdinalIgnoreCase)
            ? "#microsoft.graph.policyLocationUrl"
            : appLocationType.Equals("Domain", StringComparison.OrdinalIgnoreCase)
                ? "#microsoft.graph.policyLocationDomain"
                : "#microsoft.graph.policyLocationApplication";

        var nowIso = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        return new JsonObject
        {
            ["contentToProcess"] = new JsonObject
            {
                ["contentEntries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["@odata.type"]      = "#microsoft.graph.processConversationMetadata",
                        ["identifier"]       = contentEntryId,
                        ["content"]          = new JsonObject
                        {
                            ["@odata.type"] = "#microsoft.graph.textContent",
                            ["data"]        = text
                        },
                        ["name"]             = $"{appName} {activity}",
                        ["correlationId"]    = conversationCorrelationId ?? Guid.NewGuid().ToString(),
                        ["sequenceNumber"]   = sequenceNumber,
                        ["isTruncated"]      = false,
                        ["createdDateTime"]  = nowIso,
                        ["modifiedDateTime"] = nowIso
                    }
                },
                ["activityMetadata"]    = new JsonObject { ["activity"] = activity },
                ["deviceMetadata"]      = new JsonObject
                {
                    ["deviceType"] = "Managed",
                    ["operatingSystemSpecifications"] = new JsonObject
                    {
                        ["operatingSystemPlatform"] = Environment.OSVersion.Platform.ToString(),
                        ["operatingSystemVersion"]  = Environment.OSVersion.Version.ToString()
                    },
                    ["ipAddress"] = "127.0.0.1"
                },
                ["protectedAppMetadata"] = new JsonObject
                {
                    ["name"]    = appName,
                    ["version"] = appVersion,
                    ["applicationLocation"] = new JsonObject
                    {
                        ["@odata.type"] = locationOdataType,
                        ["value"]       = appLocationValue
                    }
                },
                ["integratedAppMetadata"] = new JsonObject
                {
                    ["name"]    = appName,
                    ["version"] = appVersion
                }
            }
        };
    }
}

/// <summary>
/// Lightweight parsed view of a Purview <c>processContent</c> response.
/// </summary>
internal sealed record PurviewProcessContentVerdict(
    bool RestrictAccess,
    string? RestrictionAction,  // block | warn | audit | null
    string? ProtectionScopeState,
    IReadOnlyList<string> RawActionTypes,
    IReadOnlyList<string> ProcessingErrors)
{
    public static PurviewProcessContentVerdict Empty { get; } =
        new(false, null, null, Array.Empty<string>(), Array.Empty<string>());

    public static PurviewProcessContentVerdict Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? scopeState = root.TryGetProperty("protectionScopeState", out var sp) ? sp.GetString() : null;
        var actionTypes = new List<string>();
        var errors = new List<string>();
        bool restrict = false;
        string? restrictAction = null;

        if (root.TryGetProperty("policyActions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in actions.EnumerateArray())
            {
                if (a.TryGetProperty("@odata.type", out var otype))
                    actionTypes.Add(otype.GetString() ?? "");
                if (a.TryGetProperty("action", out var act) && string.Equals(act.GetString(), "restrictAccess", StringComparison.OrdinalIgnoreCase))
                {
                    restrict = true;
                    if (a.TryGetProperty("restrictionAction", out var ra))
                        restrictAction = ra.GetString();
                }
            }
        }

        if (root.TryGetProperty("processingErrors", out var errs) && errs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in errs.EnumerateArray())
                errors.Add(e.GetRawText());
        }

        return new PurviewProcessContentVerdict(restrict, restrictAction, scopeState, actionTypes, errors);
    }
}
