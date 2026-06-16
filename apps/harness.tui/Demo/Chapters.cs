namespace YourCustomAgentHarness.Tui.Demo;

using YourCustomAgentHarness.Tui.Runtime;

/// <summary>
/// The 5 chapter / 12 step demo flow. All content lives here so a presenter
/// can revise wording (or skip steps) by editing this one file the night
/// before the workshop.
/// </summary>
public static class Chapters
{
    public static IReadOnlyList<Chapter> All { get; } = Build();

    private static IReadOnlyList<Chapter> Build()
    {
        var ts = TenantContext.Load();

        // Pulled from tenant-state.yaml so the live demo queries YOUR hub app, not a baked-in id.
        var hubAppId = ts.EntraApps.FirstOrDefault()?.AppId ?? "00000000-0000-0000-0000-000000000000";

        // Recurring portal links reused across multiple steps
        var portalAdminAgents = new PortalLink(
            "M365 admin center · Agents",
            "https://admin.microsoft.com/Adminportal/Home#/agents",
            "where IT sees ForgedAgentOne registered tenant-wide");
        var portalEntraBlueprints = new PortalLink(
            "Entra ID · Agent ID Blueprints",
            "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/AgentIdBlueprintMenuBlade",
            "blueprint = identity template; defines name/owner/scopes");
        var portalEntraAgents = new PortalLink(
            "Entra ID · Agent identities",
            "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/AgentIdMenuBlade",
            "the minted, runnable identity behind ForgedAgentOne");
        var portalPurview = new PortalLink(
            "Purview · DSPM for AI",
            "https://purview.microsoft.com/datasecurity/dspmforai",
            "policies governing what agents see and emit");
        var portalFoundry = new PortalLink(
            "Azure AI Foundry · your-foundry-account",
            $"https://ai.azure.com/?wsid=/subscriptions/{ts.Subscription.Id}/resourceGroups/{ts.Foundry.ResourceGroup}/providers/Microsoft.CognitiveServices/accounts/{ts.Foundry.AccountName}",
            "deployments backing the harness");
        var portalDefender = new PortalLink(
            "Defender for Cloud Apps · Agents",
            "https://security.microsoft.com/agents",
            "anomaly detection for agent identities");

        return new List<Chapter>
        {
            // ─── CHAPTER 1 ─────────────────────────────────────────────────────────
            new()
            {
                Number = 1,
                Title = "Identity & Blueprint",
                Subtitle = "How your custom harness inherits identity governance from Entra Agent ID.",
                Steps = new List<Step>
                {
                    new()
                    {
                        Number = 1, Chapter = 1, ChapterTitle = "Identity & Blueprint",
                        Title = "Boot the harness — load the agent's blueprint manifest",
                        Intro =
                            "Every agent the harness spins up starts from a [bold]blueprint manifest[/] — a human-readable YAML file " +
                            "that names the agent, lists its owner and sponsor, declares its permissions, and pins its model. " +
                            "In a bank you want this to be reviewable in code, signed by the owner, and tracked in source control. " +
                            "Watch the harness load that file before anything else happens.",
                        LiveCommand = "harness status   # shows what's running + reads tenant-state.yaml",
                        Executable = "dotnet",
                        Args = new[] { "run", "--project", Path.Combine(HarnessPaths.AppsDir, "harness.tui", "harness.tui.csproj"), "--no-launch-profile", "--", "status" },
                        WorkingDirectory = HarnessPaths.RepoRoot,
                        Timeout = TimeSpan.FromSeconds(30),
                        DefaultMode = StepMode.Scripted,
                        ScriptedOutput = $"""
[harness] reading blueprint  blueprints/forged-agent-one.harness.yaml
[harness] blueprint id       forged-agent-one
[harness] display name       ForgedAgentOne
[harness] owner              admin@example.org
[harness] sponsor            admin@example.org
[harness] auth mode          obo (delegated, on-behalf-of)
[harness] model              gpt-4.1 @ https://your-foundry-account.cognitiveservices.azure.com/
[harness] permissions        User.Read · Mail.Read · Calendars.Read · Files.Read.All · Sites.Read.All
[harness] mcp servers        mcp_MailTools · mcp_CalendarTools · mcp_SharePointTools
[harness] purview            enabled · block both directions · regex fallback
[ok] blueprint validated against schema v1
""",
                        BulletPoints = new[]
                        {
                            "[bold]The blueprint is the customer-owned source of truth[/] — it lives in their repo, reviewed like code.",
                            "Everything downstream — Entra app, admin center registration, Purview policy targeting — is derived from this one file.",
                            "Notice [bold]owner & sponsor[/] are concrete UPNs. That's how Entra Agent ID propagates lifecycle responsibility.",
                            "If a banker leaves, ownership is re-assigned by editing the manifest and re-pushing — auditable.",
                        },
                        AutoAdvance = false,
                    },
                    new()
                    {
                        Number = 2, Chapter = 1, ChapterTitle = "Identity & Blueprint",
                        Title = "Sign into the tenant (device code / interactive)",
                        Intro =
                            "Before any [bold]a365[/] subcommand can hit your tenant, we need an Azure CLI session signed in as " +
                            "an account that holds the [italic]Agent ID Developer[/] role (or Global Admin). " +
                            "The harness piggybacks on the operator's [bold]az[/] context — no service principal secrets are stored on disk.",
                        LiveCommand = "az account show --query \"{user:user.name, tenant:tenantId, sub:name}\" --output table",
                        Executable = Probes.ResolveAz(),
                        Args = new[] { "account", "show", "--query", "{user:user.name, tenant:tenantId, sub:name}", "--output", "table" },
                        Timeout = TimeSpan.FromSeconds(10),
                        DefaultMode = StepMode.Live,
                        ScriptedOutput = """
User                  Tenant                                  Sub
--------------------  --------------------------------------  ----------------------
admin@example.org       00000000-0000-0000-0000-000000000000    Istanbul
""",
                        BulletPoints = new[]
                        {
                            "Same identity that runs the demo runs the provisioning — auditable in Entra sign-in logs.",
                            "Customers can swap interactive sign-in for [bold]az login --use-device-code[/] for headless / WSL workflows (the a365 CLI reuses the az / MSAL session).",
                            "No keys or secrets ever land in source control.",
                        },
                    },
                    new()
                    {
                        Number = 3, Chapter = 1, ChapterTitle = "Identity & Blueprint",
                        Title = "Register the blueprint — mint the Entra Agent ID",
                        Intro =
                            "The CLI takes your blueprint manifest and creates two artefacts in Entra: " +
                            "(1) a [bold]Blueprint object[/] — the identity template, and (2) an [bold]Agent Identity[/] — " +
                            "the concrete service-principal-shaped object the running agent uses. " +
                            "Permissions are inheritable from the blueprint, so a fleet of agents stays consistent.\n\n" +
                            "[grey](In rehearsal we keep this step scripted because real provisioning takes 30-60s and may " +
                            "trip on tenant-side throttling. The script-tagged output is what you'd see live.)[/]",
                        LiveCommand = "a365 setup blueprint --agent-name ForgedAgentOne --authmode obo",
                        Executable = Probes.ResolveA365(),
                        Args = new[] { "setup", "blueprint", "--agent-name", "ForgedAgentOne", "--authmode", "obo", "--skip-requirements" },
                        Timeout = TimeSpan.FromSeconds(90),
                        DefaultMode = StepMode.Scripted,
                        ScriptedOutput = """
[a365] resolving Agent 365 CLI client app                     ok (a365-cli)
[a365] creating Agent ID Blueprint                            "ForgedAgentOne Blueprint"
       blueprintAppId      bdc88c0a-12bb-44a1-aa0a-9eff2cf18801
       blueprintObjectId   3f2b1c9a-8e3f-4b4b-9b2d-0fa1e7b9c4e2
[a365] minting Agent Identity from blueprint                  "ForgedAgentOne Identity"
       agentIdentityAppId  ff204e93-22d8-44bc-95b0-7c44d1a5a4f1
       agentIdentityOid    87a3115c-2d8e-4b1c-9c8d-5c2f86b3e3b5
[a365] requesting delegated grants on Microsoft Graph         (admin consent required)
       - User.Read                  granted
       - Mail.Read                  granted
       - Calendars.Read             granted
       - Files.Read.All             granted
       - Sites.Read.All             granted
[a365] writing local config                                   .a365/forged-agent-one.config.json
✓ blueprint + identity ready · run `a365 setup permissions mcp` next
""",
                        BulletPoints = new[]
                        {
                            "[bold]Two objects[/], not one. The blueprint is reusable; the identity is what runs.",
                            "Tenant-wide admin consent is requested here — IT sees a [italic]single[/] approval banner instead of per-deploy ones.",
                            "All scopes are sourced from the YAML — no hidden over-permissioning.",
                            "Re-running this command is idempotent: existing objects are detected and reused.",
                        },
                        PortalLinks = new[] { portalEntraBlueprints, portalEntraAgents },
                        OnSuccess = (state, output) =>
                        {
                            // Best-effort: scrape AppIds from output if present
                            ScrapeAndStash(output, "blueprintAppId", v => state.BlueprintAppIds["ForgedAgentOne"] = v);
                            ScrapeAndStash(output, "agentIdentityAppId", v => state.AgentAppIds["ForgedAgentOne"] = v);
                            state.Save();
                        },
                    },
                    new()
                    {
                        Number = 4, Chapter = 1, ChapterTitle = "Identity & Blueprint",
                        Title = "Inspect the minted agent identity in Entra (via Graph)",
                        Intro =
                            "Let's confirm the new objects exist in Entra without leaving the terminal. " +
                            "We query [bold]az ad app show[/] for the SPA hub app — the same object we'll see in the portal " +
                            "when we open Entra in the next step.",
                        LiveCommand = "az ad app show --id " + hubAppId + " --query \"{name:displayName, appId:appId, oid:id}\" --output table",
                        Executable = Probes.ResolveAz(),
                        Args = new[] { "ad", "app", "show", "--id", hubAppId, "--query", "{name:displayName, appId:appId, oid:id}", "--output", "table" },
                        Timeout = TimeSpan.FromSeconds(15),
                        DefaultMode = StepMode.Live,
                        ScriptedOutput = """
Name                                  AppId                                 Oid
------------------------------------  ------------------------------------  ------------------------------------
AgenticBank Hub (CustomAgentHarness)  44444444-4444-4444-4444-444444444444  55555555-5555-5555-5555-555555555555
""",
                        BulletPoints = new[]
                        {
                            "Confirmation: the harness didn't make this up — it's a real Entra app, queryable like any other.",
                            "[bold]Sign-in logs, conditional access, risk policies — all of it now applies to agents.[/]",
                            $"Click [{Theme.Amber}][[1]][/] to open the blueprint in the portal; [{Theme.Amber}][[2]][/] for the identity itself.",
                        },
                        PortalLinks = new[] { portalEntraBlueprints, portalEntraAgents },
                    },
                },
            },

            // ─── CHAPTER 2 ─────────────────────────────────────────────────────────
            new()
            {
                Number = 2,
                Title = "Model & Runtime",
                Subtitle = "Binding to Foundry-hosted models and booting the agent process.",
                Steps = new List<Step>
                {
                    new()
                    {
                        Number = 5, Chapter = 2, ChapterTitle = "Model & Runtime",
                        Title = "Verify the Foundry deployment that backs ForgedAgentOne",
                        Intro =
                            "The blueprint pins a model deployment name (gpt-4.1) and endpoint (your-foundry-account). " +
                            "Let's verify that deployment actually exists in Foundry and read off its capacity / version. " +
                            "[bold]This step does not mutate anything[/] — it's verification only, which is the safest pattern " +
                            "for a customer demo: never change cloud state from a stepped demo without explicit consent.",
                        LiveCommand = $"az cognitiveservices account deployment list -g {ts.Foundry.ResourceGroup} -n {ts.Foundry.AccountName} --query \"[].{{name:name, model:properties.model.name, capacity:sku.capacity}}\" --output table",
                        Executable = Probes.ResolveAz(),
                        Args = new[]
                        {
                            "cognitiveservices", "account", "deployment", "list",
                            "-g", ts.Foundry.ResourceGroup,
                            "-n", ts.Foundry.AccountName,
                            "--query", "[].{name:name, model:properties.model.name, capacity:sku.capacity}",
                            "--output", "table"
                        },
                        Timeout = TimeSpan.FromSeconds(20),
                        DefaultMode = StepMode.Live,
                        ScriptedOutput = """
Name                     Model                     Capacity
-----------------------  ------------------------  ----------
gpt-4.1                  gpt-4.1                   30
gpt-5.1                  gpt-5.1                   20
text-embedding-3-small   text-embedding-3-small    50
""",
                        BulletPoints = new[]
                        {
                            "Customer keeps the model in [bold]their[/] Foundry account — Microsoft never sees the prompts/responses.",
                            "Verify-only: a banking customer wants change-windows for any cloud mutation, not [italic]drive-by[/] changes.",
                            "The deployment + endpoint are baked into the agent's blueprint — drift here would fail the next step.",
                        },
                        PortalLinks = new[] { portalFoundry },
                    },
                    new()
                    {
                        Number = 6, Chapter = 2, ChapterTitle = "Model & Runtime",
                        Title = "Boot the agent processes — your harness + Agent 365 SDK",
                        Intro =
                            "Now the actual agent process starts: an ASP.NET Core listener that integrates the [bold]Agent 365 SDK[/] " +
                            "for identity + observability, plus [bold]Semantic Kernel[/] for orchestration. " +
                            "When it comes up, watch the OpenTelemetry boot trace flow into the harness Activity Console — that's " +
                            "what the customer's existing SRE tooling will ingest the same way it ingests any other ASP.NET service.",
                        LiveCommand = "harness up   # starts api, web, kb-mcp, ForgedAgentOne, ForgedScholarTwo",
                        Executable = null,    // talked through — actual `up` was already run before demo
                        ScriptedOutput = """
[harness] reading process spec from harness.tui          5 services
[harness] starting harness.api on :4000                  pid 14820  ready in 1.2s
[harness] starting harness.web on :4001                  pid 14914  ready in 4.1s
[harness] starting customagentharness-kb-mcp on :3981    pid 15008  ready in 0.9s
[harness] starting ForgedAgentOne on :3979               pid 15102
[ForgedAgentOne] boot · blueprint=forged-agent-one
[ForgedAgentOne] otel · resource attrs: agent.id=forged-agent-one  harness=CustomAgentHarness
[ForgedAgentOne] kernel · binding deploymentName=gpt-4.1 endpoint=https://your-foundry-account.cognitiveservices.azure.com/
[ForgedAgentOne] content-protection · mode=Auto purview-app-location=37573fec-… fallback=regex
[ForgedAgentOne] hosted on http://localhost:3979 · health=/api/health
[harness] starting ForgedScholarTwo on :3980             pid 15196  ready in 2.0s
[harness] ✓ all 5 services healthy
""",
                        BulletPoints = new[]
                        {
                            "[bold]The Agent 365 SDK is just a NuGet package[/] — drops into the customer's existing host.",
                            "OTel events from this process flow into harness.api via /api/_ingest, then SSE to the web UI.",
                            "Activity Console at [bold]http://localhost:4001/admin/activity[/] shows everything in real time.",
                            "Crucially: the model API key is read from env / appsettings — never touched by Microsoft.",
                        },
                        PortalLinks = new[]
                        {
                            new PortalLink("Harness Activity Console", "http://localhost:4001/admin/activity", "live OTel stream from all 5 services"),
                            new PortalLink("Admin home", "http://localhost:4001/admin", "agents · blueprints · portals"),
                        },
                    },
                },
            },

            // ─── CHAPTER 3 ─────────────────────────────────────────────────────────
            new()
            {
                Number = 3,
                Title = "Tools & Publishing",
                Subtitle = "Adding governed Work IQ tools and pushing the agent into M365 admin center.",
                Steps = new List<Step>
                {
                    new()
                    {
                        Number = 7, Chapter = 3, ChapterTitle = "Tools & Publishing",
                        Title = "Attach Work IQ MCP servers (Mail / Calendar / SharePoint)",
                        Intro =
                            "Out of the box ForgedAgentOne has no tools. We attach Microsoft's hosted [bold]Work IQ[/] MCP servers " +
                            "for Mail, Calendar and SharePoint — same surface area as Copilot, brokered by Microsoft, with " +
                            "the user's delegated scope honoured every call.",
                        LiveCommand = "a365 develop add-mcp-servers mcp_MailTools mcp_CalendarTools mcp_SharePointTools",
                        Executable = Probes.ResolveA365(),
                        Args = new[] { "develop", "add-mcp-servers", "mcp_MailTools", "mcp_CalendarTools", "mcp_SharePointTools" },
                        Timeout = TimeSpan.FromSeconds(45),
                        DefaultMode = StepMode.Scripted,
                        ScriptedOutput = """
[a365] resolving MCP catalog entries                          3 matches
       mcp_MailTools         Work IQ · Mail.All        delegated
       mcp_CalendarTools     Work IQ · Calendar.All    delegated
       mcp_SharePointTools   Work IQ · SharePoint.All  delegated
[a365] writing tooling manifest                               .a365/ToolingManifest.json (3 servers)
[a365] requesting delegated grants on Work IQ                 (admin consent required)
       - McpServers.Mail.All        granted
       - McpServers.Calendar.All    granted
       - McpServers.SharePoint.All  granted
✓ tools attached. ForgedAgentOne can now read mail / calendar / SharePoint under delegated scope.
""",
                        BulletPoints = new[]
                        {
                            "MCP tools are [bold]declared in the manifest[/] — same governance pattern as identity.",
                            "Microsoft brokers Graph API calls on the MCP server side; agents never see raw tokens for those tools.",
                            "Adding a new tool tomorrow = one YAML line + `a365 develop add-mcp-servers`.",
                        },
                        PortalLinks = new[] { portalEntraAgents },
                    },
                    new()
                    {
                        Number = 8, Chapter = 3, ChapterTitle = "Tools & Publishing",
                        Title = "Publish the agent to Microsoft 365 admin center (dry-run)",
                        Intro =
                            "Last step in identity-land: package the agent's manifest and push it into the M365 admin center so " +
                            "IT admins see [bold]ForgedAgentOne[/] alongside Copilot, third-party agents and anything else " +
                            "registered against the tenant. We dry-run here to show the change without touching production.",
                        LiveCommand = "a365 publish --agent-name ForgedAgentOne --use-blueprint --dry-run",
                        Executable = Probes.ResolveA365(),
                        Args = new[] { "publish", "--agent-name", "ForgedAgentOne", "--use-blueprint", "--dry-run" },
                        Timeout = TimeSpan.FromSeconds(60),
                        DefaultMode = StepMode.Scripted,
                        ScriptedOutput = """
[a365] loading agent config                                   .a365/forged-agent-one.config.json
[a365] resolving manifest IDs                                 (dry-run · no mutations)
       app id              ff204e93-22d8-44bc-95b0-7c44d1a5a4f1
       package name        ForgedAgentOne_1.0.0.zip
[a365] would update                                           manifest.json (icon, sponsor, publisher)
[a365] would create                                           dist/ForgedAgentOne_1.0.0.zip
✓ dry-run ok. Re-run without --dry-run to actually create the package, then upload via admin.microsoft.com.
""",
                        BulletPoints = new[]
                        {
                            "[bold]Package + upload is the same flow as any third-party agent[/] — IT keeps one onboarding workflow.",
                            "The dry-run prints exactly what would change so security can review before the real push.",
                            "Once uploaded, the agent appears under [bold]Settings → Integrated apps → Agents[/] in admin center.",
                        },
                        PortalLinks = new[] { portalAdminAgents },
                    },
                },
            },

            // ─── CHAPTER 4 ─────────────────────────────────────────────────────────
            new()
            {
                Number = 4,
                Title = "Governance & Purview",
                Subtitle = "Gating prompts in both directions with a real Purview DLP rule.",
                Steps = new List<Step>
                {
                    new()
                    {
                        Number = 9, Chapter = 4, ChapterTitle = "Governance & Purview",
                        Title = "Visit the governance portals (numbered menu — pick what to show)",
                        Intro =
                            "Before we trigger the block demo, let's pop the 4 governance surfaces a banking CISO actually cares about. " +
                            "[bold]M365 admin[/] for the registration, [bold]Entra[/] for the identity, [bold]Purview[/] for the policy, " +
                            "[bold]Defender[/] for runtime telemetry. " +
                            "Use the number keys below to open one at a time; enter to move on.",
                        Executable = null,
                        BulletPoints = new[]
                        {
                            "Each surface is the [bold]same one[/] the customer already uses for users and Copilot — nothing new to learn.",
                            "Agent governance reuses existing Conditional Access / Insider Risk / DSPM investment.",
                        },
                        PortalLinks = new[] { portalAdminAgents, portalEntraBlueprints, portalEntraAgents, portalPurview, portalDefender },
                    },
                    new()
                    {
                        Number = 10, Chapter = 4, ChapterTitle = "Governance & Purview",
                        Title = "Purview block test — both directions, four scripted prompts",
                        Intro =
                            "ForgedAgentOne's /chat path runs every inbound prompt through [bold]Purview.processContent (uploadText)[/] " +
                            "and every outbound reply through [bold]downloadText[/]. " +
                            "When the harness's Purview mode is [italic]Auto[/], it tries Microsoft Graph first and falls back to a " +
                            "deterministic regex SIT classifier if Graph fails. " +
                            "[bold]Watch the badge top-right of the chat UI[/]: REAL vs FALLBACK tells the audience which path actually fired.",
                        LiveCommand =
                            "open http://localhost:4001/hub → run the four pre-loaded prompts:\n" +
                            "  1. \"summarise my unread emails\"                  (allowed)\n" +
                            "  2. \"my card is 4532 6677 8521 3500, store it\"   (BLOCKED user→agent · credit card SIT)\n" +
                            "  3. \"draft me a status email about onboarding\"   (allowed; outbound clean)\n" +
                            "  4. (agent generates a synthetic IBAN)            (BLOCKED agent→user · IBAN SIT)",
                        Executable = null,
                        BulletPoints = new[]
                        {
                            "[bold]Both directions[/]: a hostile prompt AND a leaking response are blocked. Symmetric defence.",
                            "Block message tells the user [italic]which SIT matched[/] so they can self-correct.",
                            "Falls back to deterministic regex if Graph hiccups — never silently fails open.",
                            "Every block produces a Purview Activity Explorer event — same surface as user-side blocks today.",
                        },
                        PortalLinks = new[]
                        {
                            new PortalLink("Hub UI (end-user)", "http://localhost:4001/hub?agent=forged-agent-one", "open in your end-user browser profile"),
                            portalPurview,
                            new PortalLink("Activity Console", "http://localhost:4001/admin/activity", "watch the purview.inbound / purview.outbound events fire"),
                        },
                    },
                },
            },

            // ─── CHAPTER 5 ─────────────────────────────────────────────────────────
            new()
            {
                Number = 5,
                Title = "Hub Handoff",
                Subtitle = "End-user experience — delegated OBO and app-permission patterns side by side.",
                Steps = new List<Step>
                {
                    new()
                    {
                        Number = 11, Chapter = 5, ChapterTitle = "Hub Handoff",
                        Title = "OBO demo — sign in as the banker, ask ForgedAgentOne about your mail",
                        Intro =
                            "Switch to the [bold]end-user browser profile[/] and sign in to /hub with admin@example.org. " +
                            "MSAL.js acquires a delegated Microsoft Graph token for Mail.Read / Calendars.Read / Files.Read.All / " +
                            "Sites.Read.All and forwards it to ForgedAgentOne. " +
                            "The agent calls Graph with that token — so the user can only see what [italic]they[/] can see. " +
                            "Try [bold]\"summarise my unread emails\"[/] then [bold]\"what's on my calendar today?\"[/].",
                        Executable = null,
                        BulletPoints = new[]
                        {
                            "[bold]Identity stays user-scoped end-to-end[/] — banker permissions, not agent permissions.",
                            "Token expiry & revocation flow naturally — admin disabling the user kills the agent's reach instantly.",
                            "Same UI a customer would build for any internal AI assistant; nothing Agent-365-specific in the front-end.",
                        },
                        PortalLinks = new[]
                        {
                            new PortalLink("Hub UI — ForgedAgentOne", "http://localhost:4001/hub?agent=forged-agent-one", "delegated-permission chat"),
                            new PortalLink("Entra · sign-in logs", "https://entra.microsoft.com/#view/Microsoft_AAD_IAM/SignInEventsBlade", "see the just-now sign-in event"),
                        },
                    },
                    new()
                    {
                        Number = 12, Chapter = 5, ChapterTitle = "Hub Handoff",
                        Title = "App-permission demo — ForgedScholarTwo answers KB questions with its own identity",
                        Intro =
                            "Same hub, second agent. ForgedScholarTwo runs under [bold]application permissions[/] — it has its own " +
                            "Entra identity with its own scopes, and answers from the local AgenticBank KB MCP server. " +
                            "No user token forwarded; instead the agent's identity is what's audited. " +
                            "Ask: [bold]\"what's our KYC threshold for high-risk customers?\"[/] — note the [bold]citations[/] back to the local KB.",
                        Executable = null,
                        BulletPoints = new[]
                        {
                            "[bold]Two patterns, one harness[/]: delegated when acting for a person, app-perm when acting as a service.",
                            "Citations are sourced from the MCP server — answers are [italic]traceable[/] to specific policy docs.",
                            "Both agents share the same identity/governance/Purview substrate — pick the pattern per use case.",
                            "Closing message: [bold]the customer's harness keeps its runtime[/]. Microsoft governs identity, content and observability.",
                        },
                        PortalLinks = new[]
                        {
                            new PortalLink("Hub UI — ForgedScholarTwo", "http://localhost:4001/hub?agent=forged-scholar-two", "app-permission chat"),
                            portalAdminAgents,
                            portalDefender,
                        },
                    },
                },
            },
        };
    }

    private static void ScrapeAndStash(string output, string key, Action<string> stash)
    {
        if (string.IsNullOrEmpty(output)) return;
        var idx = output.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return;
        var slice = output.Substring(idx + key.Length);
        // Skip non-id chars
        var start = -1;
        for (var i = 0; i < slice.Length; i++)
        {
            var c = slice[i];
            if ((char.IsLetterOrDigit(c) || c == '-'))
            {
                start = i;
                break;
            }
        }
        if (start < 0) return;
        var end = start;
        while (end < slice.Length && (char.IsLetterOrDigit(slice[end]) || slice[end] == '-'))
            end++;
        var value = slice.Substring(start, end - start);
        if (value.Length >= 32 && value.Contains('-')) stash(value);
    }
}
