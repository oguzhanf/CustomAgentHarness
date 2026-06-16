# Microsoft Agent 365 — Integration patterns for a custom agent harness

**Leave-behind handout** · AgenticBank ↔ Microsoft workshop · Print double-sided.

---

## Executive summary (½ page)

You already invested in a **custom agent harness** that handles memory, knowledge, execution, and tool orchestration for your agents. You do not need to throw it away to adopt Microsoft Agent 365.

Agent 365 plugs into your harness across **five planes** that solve enterprise-readiness problems your harness is unlikely to ever build natively:

| Plane | What it gives you | Where it lives |
|---|---|---|
| **Identity** | Every agent gets an Entra **Agent Identity** minted from a **Blueprint**. Targetable by Conditional Access, audited like a service principal, ownable + transferable. | Entra → Agent IDs |
| **Governance** | Admin Center lists every agent. Owner + sponsor enforced. Approval flow for new definitions. | admin.microsoft.com → Copilot → Agents |
| **Content protection** | Microsoft Purview SDK classifies and blocks sensitive prompts and responses, **bidirectionally**. Uses your existing SIT catalog. | Purview portal → Data Map + Activity Explorer |
| **Observability** | OpenTelemetry exporter ships per-step traces to Agent 365. Correlates with sign-ins and audit. | (Agent 365 console / Defender) |
| **Threat protection** | Defender for Cloud Apps + Defender for AI surface anomalous agent behaviour, prompt injection attempts, and data exfiltration. | security.microsoft.com |

The harness keeps doing what it already does best — agent runtime, MCP tools, retrieval, hosting. Agent 365 makes that harness governable.

---

## Reference architecture (1 page)

```
┌─────────────────────────── AgenticBank tenant ────────────────────────────┐
│                                                                            │
│   ┌── End user ──┐         ┌── Admin/Security ──┐                         │
│   │ MSAL.js     │         │ admin.microsoft.com │                         │
│   │ → /hub      │         │ entra.microsoft.com │                         │
│   └──────┬──────┘         │ purview.microsoft.com │                       │
│          │ delegated      │ security.microsoft.com │                      │
│          │ user token     └──────────┬────────────┘                       │
│          ▼                            │                                    │
│  ┌──────────────────────────── YourCustomAgentHarness ─────────────────┐  │
│  │                                                                       │  │
│  │  harness.web (Next.js)   harness.api (.NET, OTel SSE)                │  │
│  │      └─ chat ──────────────┐                                          │  │
│  │                            ▼                                          │  │
│  │  ┌── ForgedAgentOne ──────────────┐  ┌── ForgedScholarTwo ────────┐ │  │
│  │  │ delegated / OBO                 │  │ application-permission     │ │  │
│  │  │ Entra Agent Identity            │  │ Entra Agent Identity       │ │  │
│  │  │ Permissions: Mail, Cal, Files,  │  │ Permissions: app-scope     │ │  │
│  │  │ Sites (delegated)               │  │ MCP: bank KB (local)       │ │  │
│  │  │ ↓ OBO exchange                  │  │ ↓ Foundry inference        │ │  │
│  │  │ Microsoft Graph                 │  │ ↓ KB retrieval (MCP)       │ │  │
│  │  └────────────┬────────────────────┘  └────────────┬───────────────┘ │  │
│  │               │                                     │                  │  │
│  │               ├── Purview SDK ──┬─────────────────┤                  │  │
│  │               │  (in + out)     │                                    │  │
│  │               ↓                  ↓                                    │  │
│  │      DefaultAzureCredential → Azure OpenAI (Foundry, no API keys)    │  │
│  └─────────────────────────┬─────────────────────────────────────────────┘  │
│                            │                                                │
│   ┌── Microsoft backplane (Agent 365) ──────────────────────────────────┐  │
│   │  Entra Agent ID  ·  Admin Center  ·  Purview  ·  Defender  ·  Audit │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Two reference identity patterns

### Pattern A — Delegated / On-Behalf-Of (OBO)

| Property | Value |
|---|---|
| Example | `ForgedAgentOne` |
| When to use | Agent acts as the signed-in user; results MUST respect that user's permissions |
| Identity in Entra | Agent Identity, but the **token used to call Graph** is OBO from the user's token |
| Permissions | **Delegated** scopes — clipped to the intersection of (blueprint declared scopes ∩ user actually has) |
| Privilege escalation | **Impossible** — agent cannot exceed what the user can do |
| Audit trail | Sign-in log shows user sign-in AND OBO exchange as separate events |

### Pattern B — Application permissions

| Property | Value |
|---|---|
| Example | `ForgedScholarTwo` |
| When to use | Agent runs without user context (background jobs, KB retrieval, scheduled tasks) |
| Identity in Entra | Agent Identity, called as itself |
| Permissions | **Application** roles — explicitly granted at blueprint registration |
| Privilege scope | Whatever the blueprint declared and admin granted — **no user gating** |
| Audit trail | Sign-in log shows agent identity sign-in directly |

**Both patterns** flow through the same Purview, audit, Defender, and admin governance plane. You pick the pattern per agent based on the threat model.

---

## What goes in a Blueprint (your security team's policy artifact)

Every agent has a YAML blueprint. The harness wraps the canonical a365 JSON in YAML for review-ability.

```yaml
metadata:
  id: forged-agent-one
  displayName: ForgedAgentOne
  tagline: "Delegated agent that acts on behalf of the user against M365."
ownership:
  owner: admin@example.org      # accountable for behaviour
  sponsor: admin@example.org    # accountable for business value
authMode: obo
permissions:
  delegated:
    - scope: graph
      scopes: [Mail.Read, Calendars.Read, Files.Read.All, Sites.Read.All]
model:
  endpoint: https://your-foundry-account.cognitiveservices.azure.com/
  deploymentName: gpt-4.1
contentProtection:
  enabled: true
  blockDirections: [user-to-agent, agent-to-user]
  sensitiveInformationTypes:
    - CreditCardNumber
    - IBAN
    - SWIFTCode
    - InternalAccountNumber    # AgenticBank custom SIT
    - CustomerTaxId
    - USSocialSecurityNumber
mcpServers:
  - id: graph_mail
    scope: delegated
hosting:
  type: dotnet-aspnet
  entryPoint: apps/ForgedAgentOne/ForgedAgentOne.csproj
```

This file is the contract. Once the security team signs off, the developer cannot change identity-, permission-, model-, or protection-related fields without re-approval.

---

## Decision checklist for each new agent

Before you ship any agent:

- [ ] **Owner + sponsor** named in the blueprint (no anonymous agents)
- [ ] **Auth mode** decided (OBO vs application) based on whether per-user data flows through
- [ ] **Declared permissions** are the *minimum* the agent needs (least-privilege)
- [ ] **Model deployment** is named (not a wildcard) so it can be deprecated independently
- [ ] **Sensitive Information Types** the agent must NOT see or emit are listed
- [ ] **MCP tools** are declared (the agent can't reach what isn't in this list)
- [ ] Blueprint registered in Entra → Agent ID Blueprints — gets approval workflow
- [ ] Agent published to **admin.microsoft.com** → governable, archivable, transferable
- [ ] **Purview** policy applied at Application location = the agent's app id
- [ ] **Defender for Cloud Apps** connector enabled for the agent identity

---

## Local quick-start (for your developers)

```powershell
# Prerequisites
winget install Microsoft.AzureCLI
npm install -g @microsoft/a365   # or: gh release download from a365 CLI repo
az login --tenant <your-tenant-id>

# Clone + run
git clone https://github.com/customer/customagentharness
cd customagentharness
.\harness.cmd doctor    # 5 green probes
.\harness.cmd up        # start everything (api, web, 2 agents, KB MCP)
.\harness.cmd status    # 5 services listening
.\harness.cmd demo      # drive the 12-step workshop demo locally
```

Open `http://localhost:4001/admin` for the harness admin UI; `http://localhost:4001/hub` for the end-user UI.

---

## FAQ (top 10 from architects and security)

**Q: Do we have to use the Microsoft Agents SDK or Semantic Kernel?**
A: No. The blueprint, Entra identity, Admin Center registration, Purview SDK, and Defender integration are all SDK-agnostic. Use any agent framework you like (LangChain, LlamaIndex, your own). The reference code uses .NET + Semantic Kernel only because it's idiomatic.

**Q: Can we host the agent process anywhere?**
A: Anywhere with Entra reachability. Bare-metal, Kubernetes, App Service, on-prem. The harness in this demo runs on the presenter's laptop — same identity story.

**Q: How does Purview SDK behave if the Purview service is unreachable?**
A: The harness's `PurviewContentProtection` wrapper supports three modes: **Real** (Purview only, errors fail-closed), **Auto** (Purview first, regex SIT fallback on failure), and **Fallback** (regex SIT only). The reference agents use Auto.

**Q: Is the OBO flow really gated by the user's permissions?**
A: Yes. The agent identity's blueprint declares the *maximum* scopes; the OBO exchange clips down to *(declared ∩ user's actual permissions)*. There is no way for the agent to exceed what the user can do.

**Q: Can we revoke an agent without code changes?**
A: Yes, three options: (1) disable the Entra Agent Identity, (2) archive the agent in admin.microsoft.com, (3) remove the blueprint approval. The harness will fail gracefully on next call.

**Q: What audit data do we get?**
A: Standard Entra sign-in logs (each agent invocation is a sign-in), Microsoft 365 audit log (Admin Center actions), Purview Activity Explorer (every classification verdict + payload metadata), and OpenTelemetry traces (in your APM or Agent 365's console).

**Q: How do we test agents in CI?**
A: Run the harness in `--demo-mode` which scripts every step deterministically against pre-recorded outputs. The reference repo includes pre-recorded fixtures.

**Q: Can two agents share a blueprint?**
A: No. One blueprint = one identity. Two agents = two blueprints = two identities. This is intentional for governance traceability.

**Q: Can we transfer ownership of an agent?**
A: Yes, in admin.microsoft.com. The Entra blueprint's `owner` and `sponsor` fields update accordingly.

**Q: What about cost attribution to the model?**
A: The Entra Agent Identity is the principal on the Azure OpenAI call. Foundry usage metrics and Azure cost management both report per-identity. Tag the resource group with the business unit for second-level reporting.

---

## Glossary

| Term | Meaning |
|---|---|
| **Agent 365 SDK / CLI** | `Microsoft.Agents.A365.*` packages + the `a365` CLI (`Microsoft.Agents.A365.DevTools.Cli`). The harness consumes Agent 365 **via the `a365` CLI** for blueprint/identity provisioning + telemetry wiring. The agent *runtime* in this repo is **Semantic Kernel** (the A365 SDK packages are not referenced directly by the agent projects). |
| **Blueprint** | The declarative spec for an agent (identity, perms, model, MCP, protection). Registered in Entra. |
| **Agent Identity** | An Entra principal minted from a blueprint. Callable as a security principal. |
| **A365 CLI (`a365`)** | The setup/publish tool. Authors blueprints, mints identities, pushes to admin center. |
| **MCP** | Model Context Protocol. Standardized way to give an agent tools/data sources. |
| **OBO** | On-Behalf-Of. Token exchange flow where the agent acts as the user with bounded permissions. |
| **SIT** | Sensitive Information Type. Purview's classifier units (CC#, SSN, IBAN, custom regex). |
| **DefaultAzureCredential** | Azure SDK credential chain that picks up `az login`, managed identity, VS, env vars — no API keys needed. |

---

*AgenticBank ↔ Microsoft workshop — leave behind v1.0 · For internal use*
