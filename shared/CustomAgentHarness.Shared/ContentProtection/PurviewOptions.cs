namespace YourCustomAgentHarness.Shared.ContentProtection;

/// <summary>
/// Binds to <c>Purview:*</c> in appsettings.json. Controls how the harness
/// invokes Microsoft Purview's data security &amp; governance Graph APIs.
/// </summary>
public sealed class PurviewOptions
{
    public const string SectionName = "Purview";

    /// <summary>
    /// One of: <c>Real</c> | <c>Fallback</c> | <c>Auto</c>.
    /// <list type="bullet">
    ///   <item><c>Real</c>: only call Graph. Errors propagate as block-by-default.</item>
    ///   <item><c>Fallback</c>: bypass Graph; use deterministic regex SIT classifier only.</item>
    ///   <item><c>Auto</c> (default for the workshop): try Graph first, fall back to regex on failure.</item>
    /// </list>
    /// </summary>
    public string Mode { get; set; } = "Auto";

    /// <summary>Display name of this harness-hosted app shown in Purview Activity Explorer.</summary>
    public string AppName { get; set; } = "CustomAgentHarness";

    /// <summary>Optional version string surfaced in Purview's <c>protectedAppMetadata</c>.</summary>
    public string AppVersion { get; set; } = "1.0.0";

    /// <summary>One of: <c>Application</c>, <c>Uri</c>, <c>Domain</c>. See policyLocation in Microsoft Graph docs.</summary>
    public string AppLocationType { get; set; } = "Application";

    /// <summary>The value tied to <see cref="AppLocationType"/>. For <c>Application</c> this is the Entra app client id of the agent.</summary>
    public string AppLocationValue { get; set; } = string.Empty;

    /// <summary>Base URI for Microsoft Graph; defaults to public cloud.</summary>
    public string GraphBaseUri { get; set; } = "https://graph.microsoft.com/v1.0/";

    /// <summary>
    /// When no end-user identity is supplied per-call (e.g. for app-only agents like ForgedScholarTwo),
    /// fall back to this user OID for Graph attribution. Typically the sponsor/admin OID.
    /// </summary>
    public string? DefaultUserObjectId { get; set; }

    /// <summary>
    /// Optional tenant id for <b>client-credential</b> auth. When <see cref="ClientId"/> +
    /// <see cref="ClientSecret"/> + <see cref="TenantId"/> are all set, the harness authenticates to
    /// Microsoft Graph as that app (e.g. the agent's own blueprint/agent identity, which holds the
    /// inheritable <c>Content.Process.*</c> / <c>ProtectionScopes.Compute.*</c> permissions) instead of
    /// reusing the operator's <c>az login</c> session. This is the production-correct path that lets the
    /// real Graph <c>processContent</c> enforcement fire. Leave blank for the dev/workshop az-login flow.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>Optional client id for client-credential auth. See <see cref="TenantId"/>.</summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional client secret for client-credential auth. <b>Never commit this.</b> Source it from an
    /// environment variable / .NET user-secrets / a key vault (e.g. <c>Purview__ClientSecret</c>).
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>If true and the Graph call returns a block verdict, the message displayed to the end user is taken from <see cref="BlockedPromptMessage"/> / <see cref="BlockedResponseMessage"/> instead of leaking what was matched.</summary>
    public bool ObfuscateMatches { get; set; } = false;

    public string BlockedPromptMessage { get; set; } = "Your message was blocked by your organization's Microsoft Purview policy.";
    public string BlockedResponseMessage { get; set; } = "The agent's response was withheld by your organization's Microsoft Purview policy.";

    /// <summary>Soft-timeout for Graph processContent calls. Above this we treat as failure and follow the <see cref="Mode"/> policy.</summary>
    public int RequestTimeoutSeconds { get; set; } = 8;
}
