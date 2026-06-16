namespace YourCustomAgentHarness.Shared.ContentProtection;

using System.Text.RegularExpressions;

/// <summary>
/// Pluggable content classifier. For the workshop demo we use a deterministic
/// regex-and-SIT classifier as a fallback for Microsoft Purview. In production
/// you would chain Purview SDK first and fall back to this only if Purview is
/// unavailable.
/// </summary>
public sealed class ContentClassifier : IContentProtection
{
    private readonly Dictionary<string, Regex> _patterns;

    public ContentClassifier(IEnumerable<string> sensitiveInformationTypes)
    {
        _patterns = new Dictionary<string, Regex>();
        foreach (var sit in sensitiveInformationTypes)
        {
            if (BuiltInPatterns.TryGetValue(sit, out var pat))
                _patterns[sit] = pat;
        }
    }

    public ClassificationResult Classify(string text, string direction)
    {
        var hits = new List<SensitiveHit>();
        foreach (var (sit, regex) in _patterns)
        {
            foreach (Match m in regex.Matches(text))
            {
                hits.Add(new SensitiveHit(sit, m.Value, m.Index, m.Length));
            }
        }
        return new ClassificationResult(
            Blocked: hits.Count > 0,
            Direction: direction,
            Hits: hits,
            ClassifierUsed: "CustomAgentHarness regex+SIT fallback",
            ClassifiedAt: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public Task<ClassificationResult> ClassifyAsync(
        string text,
        string direction,
        string? userObjectId = null,
        string? conversationId = null,
        int sequenceNumber = 0,
        CancellationToken ct = default)
    {
        return Task.FromResult(Classify(text, direction));
    }

    private static readonly Dictionary<string, Regex> BuiltInPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CreditCardNumber"]       = new Regex(@"\b(?:\d[ -]?){12,18}\d\b", RegexOptions.Compiled),
        ["IBAN"]                   = new Regex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{10,30}\b", RegexOptions.Compiled),
        ["SWIFTCode"]              = new Regex(@"\b[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?\b", RegexOptions.Compiled),
        ["InternalAccountNumber"]  = new Regex(@"\bAGB-\d{5}-\d{4}\b", RegexOptions.Compiled),
        ["CustomerTaxId"]          = new Regex(@"\b(?:\d{11}|\d{2}-\d{3}-\d{3})\b", RegexOptions.Compiled),
        ["USSocialSecurityNumber"] = new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),
        ["EmailAddress"]           = new Regex(@"\b[\w.\-]+@[\w\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled),
    };
}

public sealed record SensitiveHit(string SitName, string Value, int Index, int Length);

public sealed record ClassificationResult(
    bool Blocked,
    string Direction,
    IReadOnlyList<SensitiveHit> Hits,
    string ClassifierUsed,
    DateTimeOffset ClassifiedAt)
{
    public string Reason => Blocked
        ? $"Detected {Hits.Count} sensitive item(s): {string.Join(", ", Hits.Select(h => h.SitName).Distinct())}"
        : "Clear";
}
