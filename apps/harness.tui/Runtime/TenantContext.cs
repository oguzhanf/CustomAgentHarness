namespace YourCustomAgentHarness.Tui.Runtime;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Thin POCO mirror of <c>tenant-state.yaml</c>. Only fields the TUI actually
/// consults are deserialized; unknown fields are ignored so additions don't
/// break older builds.
/// </summary>
public sealed class TenantState
{
    public TenantBlock Tenant { get; set; } = new();
    public SubscriptionBlock Subscription { get; set; } = new();
    public FoundryBlock Foundry { get; set; } = new();
    public List<EntraApp> EntraApps { get; set; } = new();
    public DemoUsersBlock DemoUsers { get; set; } = new();
    public List<AgentBlueprintRef> AgentBlueprints { get; set; } = new();

    public sealed class TenantBlock
    {
        public string Id { get; set; } = "";
        public string Domain { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Admin { get; set; } = "";
        public string AdminObjectId { get; set; } = "";
    }

    public sealed class SubscriptionBlock
    {
        public string Id { get; set; } = "";
    }

    public sealed class FoundryBlock
    {
        public string AccountName { get; set; } = "";
        public string ResourceGroup { get; set; } = "";
        public string Region { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public List<FoundryDeployment> Deployments { get; set; } = new();
    }

    public sealed class FoundryDeployment
    {
        public string Name { get; set; } = "";
        public string? BoundTo { get; set; }
    }

    public sealed class EntraApp
    {
        public string DisplayName { get; set; } = "";
        public string Purpose { get; set; } = "";
        public string AppId { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public string? ServicePrincipalId { get; set; }
        public List<string> RedirectUris { get; set; } = new();
        public List<string> DelegatedPermissions { get; set; } = new();
        public bool AdminConsentGranted { get; set; }
    }

    public sealed class DemoUsersBlock
    {
        public DemoUser OboEndUser { get; set; } = new();
        public DemoUser FallbackUser { get; set; } = new();
    }

    public sealed class DemoUser
    {
        public string Upn { get; set; } = "";
        public string ObjectId { get; set; } = "";
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class AgentBlueprintRef
    {
        public string Id { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string BlueprintAppId { get; set; } = "";
        public string AgentIdentityAppId { get; set; } = "";
        public string? AgentIdentityObjectId { get; set; }
        public string? BlueprintObjectId { get; set; }
        public string? Status { get; set; }
    }
}

public static class TenantContext
{
    private static TenantState? _cached;

    public static TenantState Load(bool force = false)
    {
        if (!force && _cached != null) return _cached;
        if (!File.Exists(HarnessPaths.TenantStateYaml))
        {
            _cached = new TenantState();
            return _cached;
        }
        var yaml = File.ReadAllText(HarnessPaths.TenantStateYaml);
        var deser = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _cached = deser.Deserialize<TenantState>(yaml) ?? new TenantState();
        return _cached;
    }
}
