namespace YourCustomAgentHarness.Shared.Manifest;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// POCO mirror of <c>tenant-state.yaml</c> — the single, user-provided source of truth for
/// tenant/subscription/foundry/app identifiers. Loaded at runtime so NO tenant-specific value is
/// hard-coded into the harness. Unknown fields are ignored so the schema can evolve safely.
/// </summary>
public sealed class TenantState
{
    public TenantBlock Tenant { get; set; } = new();
    public SubscriptionBlock Subscription { get; set; } = new();
    public FoundryBlock Foundry { get; set; } = new();
    public List<EntraApp> EntraApps { get; set; } = new();

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
    }
}

/// <summary>
/// Locates and loads <c>tenant-state.yaml</c>. Resolution order (so a fresh clone still works):
/// <list type="number">
///   <item>explicit path / env var <c>HARNESS_TENANT_STATE</c></item>
///   <item><c>tenant-state.yaml</c> at the repo root (the user's real, git-ignored file)</item>
///   <item><c>tenant-state.example.yaml</c> at the repo root (the committed template)</item>
///   <item>an empty <see cref="TenantState"/> if nothing is found</item>
/// </list>
/// </summary>
public static class TenantStateLoader
{
    public static TenantState Load(string? startDir = null)
    {
        TenantState state;
        var path = Resolve(startDir);
        if (path is null)
        {
            state = new TenantState();
        }
        else
        {
            try
            {
                var yaml = File.ReadAllText(path);
                var deser = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                state = deser.Deserialize<TenantState>(yaml) ?? new TenantState();
            }
            catch
            {
                state = new TenantState();
            }
        }

        // .env / environment variables WIN over the yaml, so a single .env can drive everything.
        ApplyEnvOverrides(state);
        return state;
    }

    private static void ApplyEnvOverrides(TenantState s)
    {
        static string? Env(string k) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrWhiteSpace(v) ? null : v; }

        s.Tenant.Id          = Env("TENANT_ID")            ?? s.Tenant.Id;
        s.Tenant.Domain      = Env("TENANT_DOMAIN")        ?? s.Tenant.Domain;
        s.Tenant.DisplayName = Env("TENANT_DISPLAY_NAME")  ?? s.Tenant.DisplayName;
        s.Tenant.Admin       = Env("ADMIN_UPN")            ?? s.Tenant.Admin;
        s.Subscription.Id    = Env("SUBSCRIPTION_ID")      ?? s.Subscription.Id;
        s.Foundry.AccountName   = Env("FOUNDRY_ACCOUNT")        ?? s.Foundry.AccountName;
        s.Foundry.ResourceGroup = Env("FOUNDRY_RESOURCE_GROUP") ?? s.Foundry.ResourceGroup;
        s.Foundry.Region        = Env("FOUNDRY_REGION")         ?? s.Foundry.Region;
        s.Foundry.Endpoint      = Env("FOUNDRY_ENDPOINT")       ?? s.Foundry.Endpoint;

        var hubAppId = Env("HUB_APP_ID");
        if (hubAppId is not null)
        {
            if (s.EntraApps.Count == 0) s.EntraApps.Add(new TenantState.EntraApp { DisplayName = "AgenticBank Hub (CustomAgentHarness)" });
            s.EntraApps[0].AppId = hubAppId;
        }
    }

    /// <summary>Returns the path to the tenant-state file in effect, or null if none exists.</summary>
    public static string? Resolve(string? startDir = null)
    {
        var env = Environment.GetEnvironmentVariable("HARNESS_TENANT_STATE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

        var dir = startDir ?? AppContext.BaseDirectory;
        for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
        {
            var real = Path.Combine(d.FullName, "tenant-state.yaml");
            if (File.Exists(real)) return real;
            var example = Path.Combine(d.FullName, "tenant-state.example.yaml");
            if (File.Exists(example)) return example;
        }
        return null;
    }
}
