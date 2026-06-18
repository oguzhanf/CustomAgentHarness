# YourCustomAgentHarness

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Next.js 16](https://img.shields.io/badge/Next.js-16-000000?logo=nextdotjs&logoColor=white)
![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-agents-0078D4)
![Agent 365](https://img.shields.io/badge/Microsoft%20Agent%20365-preview-FF8C00)
![Entra Agent ID](https://img.shields.io/badge/Entra%20Agent%20ID-beta-8A2BE2)
![Purview](https://img.shields.io/badge/Microsoft%20Purview-DLP-1E8E3E)
![License](https://img.shields.io/badge/license-add%20one-lightgrey)

A bring-your-own-runtime reference showing how a custom agent platform plugs into the Microsoft
agent-governance stack — **Agent 365**, **Entra Agent ID**, **Purview**, and **Foundry**:

1. Register agents in a harness from declarative blueprints.
2. Provision them with the **Agent 365 CLI** (`a365`).
3. Register an **Entra Agent ID blueprint** — a reusable identity template.
4. **Mint an Agent Identity** (a real Entra `agentIdentity`) from that blueprint.
5. Govern every prompt and response through **Purview** (`processContent` + a DLP policy), grounded on a
   **Foundry** model.

| Agent | Auth | Model | Role |
|---|---|---|---|
| **ForgedAgentOne** | OBO (delegated) | `gpt-4.1` | Banker copilot — mail / calendar / SharePoint on behalf of the user |
| **ForgedScholarTwo** | S2S (application) | `gpt-5.1` | Policy scholar — answers from a local KB via MCP |

> [!NOTE]
> Microsoft **Entra Agent ID** and **Agent 365** are real but **preview/beta** in places. Out of the box
> this builds, runs, provisions a real blueprint + identity, and blocks sensitive content. Full Purview
> *enforcement* and the agent *registration / publish* step need extra tenant setup (preview enrollment) —
> see [Status](#status). Until then it degrades gracefully to a local regex classifier.

---

## Get started

From a fresh clone, **one script installs every prerequisite, builds, and runs the guided setup**:

```powershell
./setup.ps1
```

> [!NOTE]
> `setup.ps1` installs (via winget, if missing) **Git, the .NET 10 SDK, Azure CLI, Node.js, PowerShell 7**,
> the **`a365` CLI** and the **ExchangeOnlineManagement** module — then builds and runs `harness setup`.
> It can be started from Windows PowerShell 5.1: it installs PowerShell 7 and **re-launches itself** under it.
> Flags: `-SkipProvision` (install + build only), `-SkipInstall`, `-Yes` (non-interactive).

Already have the toolchain? Skip straight to the tenant flow:

```bash
az login
dotnet run --project apps/harness.tui -- setup
```

`harness setup` is the single front door. It runs every step, interactively and idempotently:

| Step | What it does |
|---|---|
| **prerequisites** | checks (and offers to install) the `a365` CLI + `ExchangeOnlineManagement` |
| **sign in** | confirms / runs `az login` |
| **configure** | **writes `.env` for you** — auto-detects tenant, subscription, UPN and Foundry from `az` |
| **roles** | grants the operator's Entra roles (Agent ID + Agent Registry + Compliance) |
| **provision** | creates the blueprints, mints the agent identities, then writes their app ids into `.env` |
| **purview** | creates the AI-app-scoped Purview DLP policy |

Re-run a single step with `--only roles,provision`; use `--yes` for non-interactive. Then:

```bash
dotnet run --project apps/harness.tui -- up      # start everything, then open http://localhost:4001
```

> [!TIP]
> Nothing is hard-coded. `harness setup` produces a git-ignored `.env`; advanced keys (model API key,
> `Purview__ClientId/ClientSecret`, per-agent overrides) are documented in
> [`.env.example`](.env.example). A YAML alternative lives in [`tenant-state.example.yaml`](tenant-state.example.yaml).

---

## What's in the repo

```
setup.ps1                      one-command bootstrap: installs prereqs, builds, runs harness setup
apps/
  harness.tui/                 the `harness` CLI: setup / up / down / status / doctor / demo
  harness.api/                 control-plane API + SSE event stream            (:4000)
  harness.web/                 Next.js admin console + agent hub (MSAL sign-in) (:4001)
  ForgedAgentOne/              reference agent — OBO / delegated                (:3979)
  ForgedScholarTwo/            reference agent — S2S / application + KB MCP     (:3980)
  customagentharness-kb-mcp/   local MCP server over the bank KB               (:3981)
shared/CustomAgentHarness.Shared/   blueprint + tenant-state + .env loaders, Purview protection, telemetry
blueprints/                    agent definitions: *.harness.yaml (authoring) + *.a365.json (a365 CLI)
kb/agenticbank/                15 markdown bank-policy docs the scholar grounds on
workshop/scripts/              grant-agent-roles - provision-agents - create-purview-policies (.ps1)
.env.example                   reference for the env vars setup writes
tenant-state.example.yaml      optional YAML alternative to .env
```

## Commands

| `dotnet run --project apps/harness.tui -- ...` | Does |
|---|---|
| `setup` | One-stop: prerequisites, `.env`, roles, provisioning, Purview (`--only` / `--skip` / `--yes`) |
| `up` / `down` / `status` | Start / stop / inspect all services |
| `doctor` | Preflight checks (az, a365, Foundry, Purview) |
| `demo` | Narrated 5-chapter workshop walkthrough |

Run one service with `dotnet run --project apps/<name>`; the web UI with `cd apps/harness.web && npm install && npm run dev`.
The provisioning scripts under `workshop/scripts/` can also be run directly (PowerShell 7).

**Prerequisites:** all installed by `./setup.ps1` — Git, the **.NET 10 SDK**, Azure CLI, PowerShell 7,
Node 20+ (web UI), the `a365` CLI and `ExchangeOnlineManagement`. Run it once, or install them yourself.

---

## Status

| Capability | State | Notes |
|---|---|---|
| Build, run, UI, blueprint + agent-identity minting | ![works](https://img.shields.io/badge/works-2EA44F) | Entra Agent ID Graph APIs are **beta** |
| Agent registration / publish | ![needs setup](https://img.shields.io/badge/needs%20setup-FF8C00) | Agent Registry Administrator + tenant **preview enrollment** |
| Purview enforcement end-to-end | ![needs setup](https://img.shields.io/badge/needs%20setup-FF8C00) | AI-app DLP policy + an identity with `Content.Process.*` (else regex fallback) |
| Purview `processContent` code | ![correct](https://img.shields.io/badge/correct-2EA44F) | matches the official v1.0 schema |

> [!IMPORTANT]
> The agent **registration / publish** step calls Graph `copilot/agentRegistrations`, which requires your
> tenant to be **enrolled in the Agent 365 / Entra Agent ID preview**. A role grant alone will not clear a
> 403 there — request onboarding through your Microsoft contact.

## References

[Agent 365](https://learn.microsoft.com/microsoft-agent-365/) &middot;
[`a365` CLI](https://learn.microsoft.com/microsoft-agent-365/developer/reference/cli/) &middot;
[Entra Agent ID](https://learn.microsoft.com/entra/agent-id/) &middot;
[Graph `processContent`](https://learn.microsoft.com/graph/api/userdatasecurityandgovernance-processcontent?view=graph-rest-1.0) &middot;
[DLP for AI apps](https://learn.microsoft.com/powershell/module/exchangepowershell/new-dlpcompliancepolicy)

## License

Add a license before publishing (for example MIT). Reference / demo code, provided as-is; the Microsoft
preview APIs it targets may change.
