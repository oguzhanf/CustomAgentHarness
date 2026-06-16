namespace YourCustomAgentHarness.Shared.ContentProtection;

/// <summary>
/// Pluggable content protection contract. Used by the harness for both
/// user → agent (uploadText) and agent → user (downloadText) evaluation.
/// Implementations include the deterministic regex/SIT fallback
/// (<see cref="ContentClassifier"/>) and the real Microsoft Purview
/// Graph processor (<see cref="PurviewContentProtection"/>).
/// </summary>
public interface IContentProtection
{
    /// <summary>
    /// Evaluate <paramref name="text"/> for sensitive content.
    /// </summary>
    /// <param name="text">The text to evaluate.</param>
    /// <param name="direction">Either <c>user-to-agent</c> (uploadText) or <c>agent-to-user</c> (downloadText).</param>
    /// <param name="userObjectId">Entra Object ID (oid) of the end user when known (required by Purview Graph API). Pass <c>null</c> for unattributed app-only calls.</param>
    /// <param name="conversationId">Optional opaque correlation id for the conversation, propagated to Purview as <c>correlationId</c>.</param>
    /// <param name="sequenceNumber">Monotonic sequence within the conversation; used by Purview for thread reconstruction.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ClassificationResult> ClassifyAsync(
        string text,
        string direction,
        string? userObjectId = null,
        string? conversationId = null,
        int sequenceNumber = 0,
        CancellationToken ct = default);
}
