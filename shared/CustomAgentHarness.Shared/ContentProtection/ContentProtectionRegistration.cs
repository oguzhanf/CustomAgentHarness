namespace YourCustomAgentHarness.Shared.ContentProtection;

using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourCustomAgentHarness.Shared.Manifest;

/// <summary>
/// DI bootstrap for the harness content-protection pipeline.
/// One call replaces the old <c>AddSingleton(new ContentClassifier(...))</c>.
/// </summary>
public static class ContentProtectionRegistration
{
    /// <summary>
    /// Register the content-protection pipeline for a single agent process.
    /// Reads <c>Purview:*</c> from configuration to decide between the real
    /// Microsoft Purview Graph processor and the regex/SIT fallback.
    /// </summary>
    public static IServiceCollection AddHarnessContentProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        AgentBlueprint blueprint)
    {
        var options = new PurviewOptions();
        configuration.GetSection(PurviewOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.AppName))
            options.AppName = blueprint.Identity.DisplayName;

        // If AppLocationValue wasn't supplied (appsettings or Purview__AppLocationValue), fall back to a
        // per-agent env var like FORGEDAGENTONE_APP_ID — so a single shared .env can drive both agents.
        if (string.IsNullOrWhiteSpace(options.AppLocationValue))
        {
            var key = (blueprint.Metadata.Name ?? options.AppName ?? "").ToUpperInvariant().Replace(" ", "") + "_APP_ID";
            var fromEnv = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(fromEnv)) options.AppLocationValue = fromEnv;
        }

        services.AddSingleton(options);
        services.AddSingleton(new ContentClassifier(blueprint.ContentProtection.SensitiveInformationTypes));

        var mode = (options.Mode ?? "Auto").Trim();
        if (string.Equals(mode, "Fallback", StringComparison.OrdinalIgnoreCase))
        {
            // Resolve IContentProtection directly to the regex classifier; no Graph wiring.
            services.AddSingleton<IContentProtection>(sp => sp.GetRequiredService<ContentClassifier>());
            return services;
        }

        // Real or Auto → wire up Graph credential + named HTTP client + Purview processor
        services.AddSingleton<TokenCredential>(_ =>
        {
            // Production path: if a client id/secret/tenant are configured, authenticate to Graph
            // as the agent's OWN identity (the blueprint/agent app that holds the inheritable
            // Content.Process.* / ProtectionScopes.Compute.* permissions). This is what lets the
            // real processContent enforcement fire without relying on the operator's az session.
            if (!string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret) &&
                !string.IsNullOrWhiteSpace(options.TenantId))
            {
                return new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
            }

            // Workshop/dev fallback: reuse az login (DeviceCode). The *operator's local az session*
            // is the calling identity. Note: the Azure CLI first-party app does not hold the Purview
            // Content.Process scopes, so in this mode the harness typically degrades to the regex SIT
            // classifier (Purview.Mode=Auto). Configure ClientId/ClientSecret/TenantId for live Purview.
            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzurePowerShellCredential(),
                new InteractiveBrowserCredential());
        });

        services.AddHttpClient<PurviewContentProtection>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(Math.Max(2, options.RequestTimeoutSeconds + 2));
        });

        services.AddSingleton<IContentProtection>(sp => sp.GetRequiredService<PurviewContentProtection>());
        return services;
    }
}
