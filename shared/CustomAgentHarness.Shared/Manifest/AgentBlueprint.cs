namespace YourCustomAgentHarness.Shared.Manifest;

using YamlDotNet.Serialization;

/// <summary>
/// Custom agent harness blueprint manifest — the customer's authoring format.
/// At provisioning time, transformed by the harness into the
/// Microsoft Agent 365 blueprint JSON expected by `a365 setup blueprint`.
/// </summary>
public sealed class AgentBlueprint
{
    [YamlMember(Alias = "apiVersion")]
    public string ApiVersion { get; set; } = "customagentharness/v1";

    [YamlMember(Alias = "kind")]
    public string Kind { get; set; } = "AgentBlueprint";

    [YamlMember(Alias = "metadata")]
    public Metadata Metadata { get; set; } = new();

    [YamlMember(Alias = "identity")]
    public Identity Identity { get; set; } = new();

    [YamlMember(Alias = "ownership")]
    public Ownership Ownership { get; set; } = new();

    [YamlMember(Alias = "lifecycle")]
    public Lifecycle? Lifecycle { get; set; }

    [YamlMember(Alias = "authMode")]
    public string AuthMode { get; set; } = "obo"; // obo | s2s | both

    [YamlMember(Alias = "permissions")]
    public Permissions Permissions { get; set; } = new();

    [YamlMember(Alias = "model")]
    public ModelBinding Model { get; set; } = new();

    [YamlMember(Alias = "mcpServers")]
    public List<McpServerRef> McpServers { get; set; } = new();

    [YamlMember(Alias = "retrieval")]
    public RetrievalConfig? Retrieval { get; set; }

    [YamlMember(Alias = "observability")]
    public ObservabilityConfig Observability { get; set; } = new();

    [YamlMember(Alias = "contentProtection")]
    public ContentProtection ContentProtection { get; set; } = new();

    [YamlMember(Alias = "hosting")]
    public HostingConfig Hosting { get; set; } = new();
}

public sealed class Metadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";

    [YamlMember(Alias = "createdBy")]
    public string CreatedBy { get; set; } = "";
}

public sealed class Identity
{
    [YamlMember(Alias = "displayName")]
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Tagline { get; set; }
    public string? Category { get; set; }

    [YamlMember(Alias = "iconUri")]
    public string? IconUri { get; set; }
    public string? Publisher { get; set; }
}

public sealed class Ownership
{
    public string Owner { get; set; } = "";
    public string Sponsor { get; set; } = "";

    [YamlMember(Alias = "costCenter")]
    public string? CostCenter { get; set; }

    [YamlMember(Alias = "businessUnit")]
    public string? BusinessUnit { get; set; }
}

public sealed class Lifecycle
{
    public string Status { get; set; } = "Active";

    [YamlMember(Alias = "reviewCycleDays")]
    public int ReviewCycleDays { get; set; } = 90;

    [YamlMember(Alias = "retireOn")]
    public string? RetireOn { get; set; }
}

public sealed class Permissions
{
    public List<ResourcePermission> Delegated { get; set; } = new();
    public List<ResourcePermission> Application { get; set; } = new();
}

public sealed class ResourcePermission
{
    public string Resource { get; set; } = "";
    public List<string> Scopes { get; set; } = new();
    public List<string> Roles { get; set; } = new();
}

public sealed class ModelBinding
{
    public string Provider { get; set; } = "AzureOpenAI";
    public string Endpoint { get; set; } = "";

    [YamlMember(Alias = "deploymentName")]
    public string DeploymentName { get; set; } = "";

    [YamlMember(Alias = "apiVersion")]
    public string? ApiVersion { get; set; }
    public double Temperature { get; set; } = 0.3;

    [YamlMember(Alias = "maxTokens")]
    public int MaxTokens { get; set; } = 2048;
}

public sealed class McpServerRef
{
    public string Id { get; set; } = "";
    public string Scope { get; set; } = "";
    public string? Rationale { get; set; }
    public string? Endpoint { get; set; }
}

public sealed class RetrievalConfig
{
    public string Provider { get; set; } = "AzureOpenAI";

    [YamlMember(Alias = "embeddingDeployment")]
    public string EmbeddingDeployment { get; set; } = "";

    [YamlMember(Alias = "chunkTokens")]
    public int ChunkTokens { get; set; } = 512;

    [YamlMember(Alias = "chunkOverlap")]
    public int ChunkOverlap { get; set; } = 64;

    [YamlMember(Alias = "topK")]
    public int TopK { get; set; } = 6;

    [YamlMember(Alias = "similarityThreshold")]
    public double SimilarityThreshold { get; set; } = 0.75;
}

public sealed class ObservabilityConfig
{
    public List<string> Exporters { get; set; } = new() { "Console", "Agent365" };

    [YamlMember(Alias = "redactPiiInDev")]
    public bool RedactPiiInDev { get; set; } = true;

    [YamlMember(Alias = "sampleRate")]
    public double SampleRate { get; set; } = 1.0;
}

public sealed class ContentProtection
{
    public bool Enabled { get; set; } = true;

    [YamlMember(Alias = "blockDirections")]
    public List<string> BlockDirections { get; set; } = new() { "user-to-agent", "agent-to-user" };

    [YamlMember(Alias = "sensitiveInformationTypes")]
    public List<string> SensitiveInformationTypes { get; set; } = new();

    [YamlMember(Alias = "fallbackClassifier")]
    public string FallbackClassifier { get; set; } = "regex";
}

public sealed class HostingConfig
{
    public string Type { get; set; } = "dotnet-aspnet";

    [YamlMember(Alias = "entryPoint")]
    public string EntryPoint { get; set; } = "";

    [YamlMember(Alias = "listenPort")]
    public int ListenPort { get; set; } = 3979;

    [YamlMember(Alias = "healthPath")]
    public string HealthPath { get; set; } = "/api/health";

    [YamlMember(Alias = "activityPath")]
    public string ActivityPath { get; set; } = "/api/messages";
}
