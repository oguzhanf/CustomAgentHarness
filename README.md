# YourCustomAgentHarness

A **bring-your-own-runtime reference** that shows how a custom agent platform plugs into the
Microsoft agent-governance stack — **Microsoft Agent 365**, **Microsoft Entra Agent ID**,
**Microsoft Purview**, and **Microsoft Foundry** — end to end:

1. **Register agents** in a harness from declarative blueprints.
2. Provision them with the **Agent 365 CLI** (`a365`).
3. Register an **Entra Agent ID *blueprint*** (a reusable identity template).
4. **Mint an Agent Identity** (a real Entra `agentIdentity` service principal) from that blueprint.
5. Govern every prompt/response through **Microsoft Purview** (Graph `processContent`) + a **DLP policy**,
   grounded on a model deployment in **Microsoft Foundry**.

Two reference agents ship in the box:

| Agent | Auth | Model | What it does |
|---|---|---|---|
| **ForgedAgentOne** | OBO (delegated) | gpt-4.1 | Banker copilot — mail / calendar / SharePoint **on behalf of the user** |
| **ForgedScholarTwo** | S2S (application) | gpt-5.1 | Policy scholar — answers from a local **KB via MCP** |

> ### ⚠️ Maturity notice — read this first
> Microsoft **Entra Agent ID** and **Agent 365** are real but **preview/beta** in places.
> Out of the box this repo **builds, runs, provisions a real blueprint + agent identity, and blocks
> sensitive content**. Two things need extra (documented) tenant setup to be *fully* live:
> **(a)** real Purview *enforcement* (a correctly-scoped DLP policy + permissions), and
> **(b)** the **agent-registration / publish** step (needs tenant **preview enrollment**).
> Without them the harness degrades gracefully (local regex content filter; the blueprint + identity
> are still created). See [Status & caveats](#status--caveats).

---

## Table of contents
- [Configure once: the `.env` file](#configure-once-the-env-file)
- [Repository layout (folder by folder)](#repository-layout-folder-by-folder)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Command reference (everything you can run)](#command-reference-everything-you-can-run)
- [Quick start — DRY RUN](#quick-start--dry-run-no-tenant)
- [Full setup — LIVE](#full-setup--live-your-tenant)
- [Configuration reference](#configuration-reference)
- [Status & caveats](#status--caveats)
- [Security & publishing checklist](#security--publishing-checklist)
- [Troubleshooting](#troubleshooting)
- [References](#references)

---

## Configure once: the `.env` file

**All environment-specific values live in one git-ignored `.env` file.** Nothing tenant-specific is
hard-coded; every app loads `.env` at startup and real shell/CI env vars always win.

```bash
cp .env.example .env      # then edit .env with your values  (Windows: Copy-Item .env.example .env)
```

`.env` is **entirely optional** — with an empty/absent `.env` the harness runs in **DRY mode**
(builds, UI works, content protection uses the local regex classifier). Fill values in to go LIVE.

| Variable | Used by | Purpose |
|---|---|---|
| `TENANT_ID` | UI, agents, scripts | Your Entra tenant id |
| `TENANT_DOMAIN` / `TENANT_DISPLAY_NAME` | UI | Shown in the status bar |
| `ADMIN_UPN` | scripts, blueprint owner | Operator / blueprint-owner UPN |
| `SUBSCRIPTION_ID` | UI portal link | Azure subscription (Foundry deep-link) |
| `FOUNDRY_ACCOUNT` / `FOUNDRY_RESOURCE_GROUP` / `FOUNDRY_REGION` / `FOUNDRY_ENDPOINT` | UI, agents | The model backend |
| `AZURE_OPENAI_API_KEY` | agents | Use a key instead of Entra auth (optional) |
| `HUB_APP_ID` | UI, demo | AgenticBank Hub SPA app id |
| `FORGEDAGENTONE_APP_ID` / `FORGEDSCHOLARTWO_APP_ID` | agents | Each agent's app id → its Purview DLP location |
| `Purview__Mode` | agents | `Auto` \| `Real` \| `Fallback` |
| `Purview__DefaultUserObjectId` | agents | Licensed user OID for Graph attribution |
| `Purview__ClientId` / `Purview__ClientSecret` | agents | Client-credential auth for **live** Purview (secret never committed) |

> **Two ways to provide the same data.** The harness UI/provisioning also reads an optional
> **`tenant-state.yaml`** (copy from [`tenant-state.example.yaml`](tenant-state.example.yaml)).
> `.env` values **override** `tenant-state.yaml`. Use whichever you prefer; `.env` is the simplest.

---

## Repository layout (folder by folder)

```
YourCustomAgentHarness/
├─ apps/                                  ← all runnable processes
│  ├─ harness.tui/          the `harness` orchestrator CLI (Spectre.Console): up / down /
│  │                        status / doctor / demo. Spawns + supervises every service.
│  ├─ harness.api/          control-plane Web API (:4000): blueprint registry, agent proxy,
│  │                        tenant summary (/api/tenant), and an SSE event stream (/api/events).
│  ├─ harness.web/          Next.js admin console + agent "hub" UI (:4001). MSAL sign-in.
│  ├─ ForgedAgentOne/       reference agent #1 — OBO/delegated banker copilot (:3979).
│  ├─ ForgedScholarTwo/     reference agent #2 — S2S/application policy scholar (:3980).
│  └─ customagentharness-kb-mcp/  local Model Context Protocol server over the KB (:3981).
│
├─ shared/CustomAgentHarness.Shared/      ← shared library used by every app
│  ├─ DotEnv.cs             the .env loader.
│  ├─ Manifest/             blueprint loader + TenantState loader (reads tenant-state.yaml / .env).
│  ├─ ContentProtection/    Microsoft Purview integration (Graph processContent) + regex SIT fallback.
│  └─ Telemetry/            activity stream + OpenTelemetry forwarding to harness.api.
│
├─ blueprints/                            ← the agent definitions (one pair per agent)
│  ├─ *.harness.yaml        rich, human-authored definition (identity, perms, model, MCP, protection).
│  └─ *.a365.json           trimmed payload the `a365` CLI consumes. Generated from the .harness.yaml.
│
├─ kb/agenticbank/                        ← 15 markdown bank-policy docs the scholar grounds on
│
├─ workshop/                              ← demo collateral + setup automation
│  ├─ scripts/
│  │  ├─ grant-agent-roles.ps1      ensure the operator's Entra roles (idempotent, reuses az).
│  │  ├─ provision-agents.ps1       create blueprints + mint identities via the a365 CLI.
│  │  └─ create-purview-policies.ps1 create the AI-app-scoped Purview DLP policy.
│  ├─ agenda.md · demo-script.md · leave-behind.md · purview-powershell-setup.md
│  └─ slides.pptx · architecture.excalidraw
│
├─ publish/                               ← per-agent a365 config + reference manifest
│                                            (a365.generated.config*.json is git-ignored — holds a secret)
├─ state/                                 ← local runtime state + per-service logs (git-ignored)
│
├─ .env.example                           ← COPY to .env and fill in (single config file)
├─ tenant-state.example.yaml              ← optional YAML alternative to .env
├─ Directory.Packages.props               ← central NuGet package versions
├─ global.json                            ← pinned .NET SDK
└─ YourCustomAgentHarness.sln
```

---

## Architecture

```
                       ┌─────────────────────────────┐
   browser ──────────► │  harness.web  :4001 (Next.js)│  admin console + agent hub
                       └──────────────┬──────────────┘
                                      │ REST + SSE
                       ┌──────────────▼──────────────┐
                       │  harness.api  :4000 (.NET)   │  blueprint registry · proxy · event stream
                       └───────┬───────────────┬──────┘
                               │  proxy /chat  │
            ┌──────────────────▼───┐   ┌───────▼───────────────┐
            │ ForgedAgentOne :3979 │   │ ForgedScholarTwo :3980│  Semantic Kernel agents
            └──────────┬───────────┘   └───────┬───────────────┘
                       │ Purview processContent │ + MCP
        ┌──────────────▼───────────┐   ┌────────▼────────────────────────┐
        │ Microsoft Graph / Purview│   │ customagentharness-kb-mcp :3981 │
        │ Microsoft Foundry (AOAI) │   │ (15 bank policy docs over MCP)  │
        └──────────────────────────┘   └─────────────────────────────────┘

Provisioning (one-time):  harness.yaml ─► a365 CLI ─► Entra Agent ID Blueprint ─► Agent Identity
Governance (per request): prompt ─► Purview processContent (DLP) ─► model ─► response ─► processContent
```

The **agent runtime is Semantic Kernel.** Agent 365 is consumed **via the `a365` CLI** for provisioning.

---

## Prerequisites

| Tool | Version | Install | Needed for |
|---|---|---|---|
| .NET SDK | 9.0.3xx | https://dotnet.microsoft.com | everything |
| Node.js | 20+ (LTS) | https://nodejs.org | `harness.web` only |
| PowerShell | 7+ | https://aka.ms/powershell | the `workshop/scripts/*.ps1` |
| Azure CLI | latest | https://aka.ms/azcli | model auth + provisioning identity |
| Agent 365 CLI | 1.1.19x+ | `dotnet tool install --global Microsoft.Agents.A365.DevTools.Cli` | LIVE provisioning |
| ExchangeOnlineManagement | 3.5+ | `Install-Module ExchangeOnlineManagement` | Purview DLP (LIVE) |

**Tenant (LIVE only):** a Microsoft Foundry account with a chat deployment (e.g. `gpt-4.1`) on which
your identity has **Cognitive Services OpenAI User**; the Entra roles ensured by
[`grant-agent-roles.ps1`](workshop/scripts/grant-agent-roles.ps1); tenant **preview enrollment** for
agent registration; and an AI-app-scoped DLP policy for live Purview enforcement.

---

## Command reference (everything you can run)

> Run the orchestrator either as `dotnet run --project apps/harness.tui -- <cmd>` or, after a build,
> the produced `harness` executable. Examples use the `dotnet run` form so they work with no setup.

### Build
| Command | What it does |
|---|---|
| `dotnet build YourCustomAgentHarness.sln` | Build all six projects. |

### Orchestrator (the `harness` TUI)
| Command | What it does |
|---|---|
| `dotnet run --project apps/harness.tui -- setup` | **One-stop guided setup** — prerequisites (with installs), `.env` config, Entra roles, provisioning, Purview DLP. Idempotent; `--only`/`--skip <steps>`, `--yes` for non-interactive. |
| `dotnet run --project apps/harness.tui -- up` | Start **all** services (api, web, kb-mcp, both agents) and wait for health. Idempotent. |
| `dotnet run --project apps/harness.tui -- up --only ForgedAgentOne` | Start only the named service(s). |
| `dotnet run --project apps/harness.tui -- status` | Port + PID + health table; prints the active tenant. |
| `dotnet run --project apps/harness.tui -- doctor` | Preflight checks (az login, a365 CLI, Foundry, Purview readiness). |
| `dotnet run --project apps/harness.tui -- demo` | The narrated 5-chapter workshop walkthrough (live, with scripted fallback). |
| `dotnet run --project apps/harness.tui -- down` | Stop everything the harness started. |

### Individual services
| Command | Port |
|---|---|
| `dotnet run --project apps/harness.api` | 4000 |
| `dotnet run --project apps/ForgedAgentOne` | 3979 |
| `dotnet run --project apps/ForgedScholarTwo` | 3980 |
| `dotnet run --project apps/customagentharness-kb-mcp` | 3981 |
| `cd apps/harness.web && npm install && npm run dev` | 4001 |

### Setup automation (PowerShell 7+, LIVE)
| Command | What it does |
|---|---|
| `pwsh workshop/scripts/grant-agent-roles.ps1 [-IncludePurview] [-WhatIfMode]` | Ensure the signed-in operator has Agent ID Developer/Administrator + Agent Registry Administrator (+ Compliance Admin). |
| `pwsh workshop/scripts/provision-agents.ps1 [-Agents ForgedScholarTwo] [-Publish]` | Create blueprint(s) + mint agent identity(ies) via the `a365` CLI; sets owner from your config. |
| `pwsh workshop/scripts/create-purview-policies.ps1 [-WhatIfMode]` | Create the AI-app-scoped Purview DLP policy that blocks bank SITs for the agents. |

### Smoke tests (curl)
```bash
curl http://localhost:3979/api/health
curl http://localhost:3979/api/identity
curl -X POST http://localhost:3979/chat -H "Content-Type: application/json" \
     -d '{"message":"Customer SSN 120-98-1437 - please verify"}'      # → blocked
curl http://localhost:4000/api/tenant     # harness view of your tenant (from .env / tenant-state.yaml)
```

---

## Quick start — DRY RUN (no tenant)

```bash
dotnet build YourCustomAgentHarness.sln
dotnet run --project apps/harness.tui -- up      # api + web + kb-mcp + both agents
#   open http://localhost:4001   ·   stop with:  ... -- down
```
Content protection works with no tenant (regex SIT classifier). For real model replies in dry mode,
set `AZURE_OPENAI_API_KEY` (or `az login` to a reachable Foundry) in `.env`. The narrated walkthrough
`... -- demo` rehearses the full live flow using scripted output.

---

## Full setup — LIVE (your tenant)

> **Fastest path — one command does it all (interactive & idempotent):**
> ```bash
> az login
> dotnet run --project apps/harness.tui -- setup
> ```
> `harness setup` walks every step below in order: checks/installs prerequisites, writes `.env`
> (auto-detecting tenant/subscription/UPN from your `az` session), ensures your Entra roles, provisions
> the blueprints + agent identities, and creates the Purview DLP policy. Re-run any step with
> `--only roles,provision`. The manual equivalents are below if you prefer to run them piecemeal.

```bash
# 1. Sign in + configure
az login
cp .env.example .env        # fill TENANT_*, FOUNDRY_*, ADMIN_UPN, etc.

# 2. Ensure operator roles (idempotent; reuses az; defaults to the signed-in user)
pwsh workshop/scripts/grant-agent-roles.ps1 -IncludePurview

# 3. Provision blueprints + mint identities (interactive a365 sign-in)
pwsh workshop/scripts/provision-agents.ps1
#    -> copy each agent's app id into .env (FORGEDAGENTONE_APP_ID / FORGEDSCHOLARTWO_APP_ID)

# 4. Create the AI-app-scoped Purview DLP policy (interactive Connect-IPPSSession)
pwsh workshop/scripts/create-purview-policies.ps1

# 5. (For real Purview enforcement) set Purview__ClientId / Purview__ClientSecret / Purview__Mode=Real
#    in .env so an agent calls Graph as its own identity.

# 6. Run + verify
dotnet run --project apps/harness.tui -- up
```
A blocked response from **real Purview** reads `…Microsoft Purview (Graph processContent → restrictAccess)`
(vs the regex `Detected N sensitive item(s)…`) and appears in **purview.microsoft.com → DSPM for AI →
Activity Explorer** with `Action = restrictAccess` (allow ~5–60 min for policy replication).

---

## Configuration reference

Three layers, highest precedence first:

1. **Real environment variables** (shell / CI) — always win.
2. **`.env`** at the repo root — the recommended place (loaded by every app via `DotEnv`).
3. **`appsettings.json`** (agents) / **`tenant-state.yaml`** (harness UI + scripts) — defaults/templates.

**Agent `Purview` config** (`apps/<agent>/appsettings.json`, or `Purview__*` in `.env`):

| Key | Meaning |
|---|---|
| `Mode` | `Auto` (Graph→regex fallback) · `Real` (Graph only) · `Fallback` (regex only) |
| `AppLocationValue` | The agent's Entra app id (DLP location). Falls back to `<AGENT>_APP_ID` env var. |
| `DefaultUserObjectId` | User OID for Graph attribution when no per-call user is supplied. |
| `TenantId` / `ClientId` / `ClientSecret` | Client-credential auth for live Purview (secret from env only). |

**Service ports:** api `4000` · web `4001` · kb-mcp `3981` · ForgedAgentOne `3979` · ForgedScholarTwo `3980`.

---

## Status & caveats

| Capability | Status | Notes |
|---|---|---|
| Build + run + harness UI | ✅ Works | `dotnet build` clean; `harness up` starts all services |
| Entra Agent ID **blueprint** | ✅ Real | Graph `agentIdentityBlueprint` — **beta** API |
| **Mint** agent identity | ✅ Real | Graph `agentIdentity` (`servicePrincipalType=ServiceIdentity`) — **beta** |
| Agent **registration / publish** | ⚠️ Preview-gated | Needs **Agent Registry Administrator** (scripted) **and tenant preview enrollment** (Microsoft-side) |
| Purview `processContent` code | ✅ Correct | Matches the official v1.0 schema |
| Purview **enforcement** end-to-end | ⚠️ Needs setup | Requires the AI-app DLP policy + an identity holding `Content.Process.*`/`ProtectionScopes.Compute.*` + replication time; otherwise the regex fallback blocks |
| DSPM-for-AI **capture** | ⚠️ Preview | `New-FeatureConfiguration -FeatureScenario KnowYourData` is public preview |
| Agent SDK | ℹ️ via CLI | Runtime is Semantic Kernel; Agent 365 is consumed through the `a365` CLI |

---

## Security & publishing checklist

- ✅ Secrets are git-ignored: `state/`, `**/a365.generated.config*.json` (DPAPI blueprint secret),
  `.env`, `**/a365.config.json`, and your real `tenant-state.yaml`. Run `git status` before the first push.
- ✅ No tenant-specific identifiers are committed — they come from `.env` / `tenant-state.yaml` (git-ignored).
  The repo ships `.env.example` + `tenant-state.example.yaml` templates with placeholders.
- 🔁 **Rotate** any blueprint client secret that was ever displayed (Entra → app → Certificates & secrets).
- 🔑 Never put a secret in `appsettings.json`. Use `.env`, `appsettings.Local.json` (git-ignored), or a key vault.

---

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `chat` blocks with a regex reason, not Purview | Expected until the live Purview steps are done. Set `Purview__Mode=Real` + client-cred in `.env`. |
| Agent registration `403 UnknownError` | Tenant not preview-enrolled (a role alone is insufficient). Request Agent 365 onboarding. |
| `a365` not found | `dotnet tool install --global Microsoft.Agents.A365.DevTools.Cli` |
| Model `401/403` | Grant your identity **Cognitive Services OpenAI User** on the Foundry account, or set `AZURE_OPENAI_API_KEY`. |
| UI shows placeholder tenant | Fill `.env` (or `tenant-state.yaml`) and restart `harness.api`. |
| Ports busy on `up` | `dotnet run --project apps/harness.tui -- down`, then retry. |

---

## References

- Microsoft Agent 365 — https://learn.microsoft.com/microsoft-agent-365/
- Agent 365 CLI (`a365`) — https://learn.microsoft.com/microsoft-agent-365/developer/reference/cli/
- Microsoft Entra Agent ID — https://learn.microsoft.com/entra/agent-id/
- Graph `agentIdentityBlueprint` (beta) — https://learn.microsoft.com/graph/api/resources/agentidentityblueprint?view=graph-rest-beta
- Graph `processContent` (Purview, v1.0) — https://learn.microsoft.com/graph/api/userdatasecurityandgovernance-processcontent?view=graph-rest-1.0
- DLP for AI apps — https://learn.microsoft.com/powershell/module/exchangepowershell/new-dlpcompliancepolicy
- Microsoft 365 Agents SDK — https://learn.microsoft.com/microsoft-365/agents-sdk/agents-sdk-overview

## License

Add a license before publishing (e.g. MIT). Reference/demo code, provided as-is; the Microsoft preview
APIs it targets are subject to change.
