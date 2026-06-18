# YourCustomAgentHarness

A bring-your-own-runtime reference showing how a custom agent platform plugs into the Microsoft
agent-governance stack — **Agent 365**, **Entra Agent ID**, **Purview**, and **Foundry**:

1. Register agents in a harness from declarative blueprints.
2. Provision them with the **Agent 365 CLI** (`a365`).
3. Register an **Entra Agent ID blueprint** (a reusable identity template).
4. **Mint an Agent Identity** (a real Entra `agentIdentity`) from that blueprint.
5. Govern every prompt/response through **Purview** (`processContent` + a DLP policy), grounded on a
   **Foundry** model.

| Agent | Auth | Model | Role |
|---|---|---|---|
| **ForgedAgentOne** | OBO (delegated) | gpt-4.1 | Banker copilot — mail/calendar/SharePoint on behalf of the user |
| **ForgedScholarTwo** | S2S (application) | gpt-5.1 | Policy scholar — answers from a local KB via MCP |

> **Heads-up:** Entra Agent ID and Agent 365 are real but **preview/beta** in places. Out of the box this
> builds, runs, provisions a real blueprint + identity, and blocks sensitive content. Full Purview
> *enforcement* and the agent *registration/publish* step need extra tenant setup (preview enrollment) —
> see [Status](#status). Until then it degrades gracefully (local regex filter).

---

## Quick start (no tenant needed)

```bash
dotnet build YourCustomAgentHarness.sln
dotnet run --project apps/harness.tui -- up      # api + web + kb-mcp + both agents
# open http://localhost:4001   ·   stop with:  ... -- down
```
With no `.env`, agents run and block sensitive content via the local regex classifier:
```bash
curl -X POST http://localhost:3979/chat -H "Content-Type: application/json" \
     -d '{"message":"Customer SSN 120-98-1437"}'      # → { "blocked": true, ... }
```

## Go live (your tenant) — one command

```bash
az login
dotnet run --project apps/harness.tui -- setup
```
`harness setup` walks every step, interactively and idempotently:
**prerequisites** (checks/installs `a365` + Exchange module) → **`.env`** (auto-fills tenant/sub/UPN from
`az`) → **Entra roles** → **provision** blueprints + mint identities → **Purview DLP policy**.
Re-run a single step with `--only roles,provision`; `--yes` for non-interactive.

---

## Configuration — one `.env` file

```bash
cp .env.example .env       # then fill in your values  (Windows: Copy-Item)
```
Every app loads `.env` at startup; real env vars override it; an empty `.env` = dry mode. Most-used keys
(see [`.env.example`](.env.example) for the full, commented list):

| Key | Purpose |
|---|---|
| `TENANT_ID`, `ADMIN_UPN`, `SUBSCRIPTION_ID` | tenant context (auto-filled by `harness setup`) |
| `FOUNDRY_ENDPOINT`, `AZURE_OPENAI_API_KEY` | the model backend (Entra auth, or a key) |
| `FORGEDAGENTONE_APP_ID`, `FORGEDSCHOLARTWO_APP_ID` | each agent's app id = its Purview DLP location |
| `Purview__Mode` (`Auto`/`Real`/`Fallback`), `Purview__ClientId/ClientSecret` | content protection |

> Prefer YAML? `tenant-state.yaml` works too (copy `tenant-state.example.yaml`). `.env` wins over it.
> Both, plus secrets, are git-ignored.

---

## What's in the repo

```
apps/
  harness.tui/                 `harness` CLI: setup / up / down / status / doctor / demo
  harness.api/                 control-plane API + SSE event stream            (:4000)
  harness.web/                 Next.js admin console + agent hub (MSAL sign-in) (:4001)
  ForgedAgentOne/              reference agent — OBO/delegated                  (:3979)
  ForgedScholarTwo/            reference agent — S2S/application + KB MCP       (:3980)
  customagentharness-kb-mcp/   local MCP server over the bank KB               (:3981)
shared/CustomAgentHarness.Shared/   blueprint + tenant-state + .env loaders, Purview protection, telemetry
blueprints/                    agent definitions: *.harness.yaml (authoring) + *.a365.json (a365 CLI)
kb/agenticbank/                15 markdown bank-policy docs the scholar grounds on
workshop/scripts/              grant-agent-roles · provision-agents · create-purview-policies (.ps1)
.env.example                   copy to .env and fill in
tenant-state.example.yaml      optional YAML alternative to .env
```

## Commands

| Command (`dotnet run --project apps/harness.tui -- …`) | Does |
|---|---|
| `setup` | One-stop: prerequisites, `.env`, roles, provisioning, Purview (`--only`/`--skip`/`--yes`) |
| `up` / `down` / `status` | Start / stop / inspect all services |
| `doctor` | Preflight checks (az, a365, Foundry, Purview) |
| `demo` | Narrated 5-chapter workshop walkthrough |

Run a single service with `dotnet run --project apps/<name>`; the web UI with `cd apps/harness.web && npm install && npm run dev`.
Provisioning scripts can also be run directly from `workshop/scripts/` (PowerShell 7).

**Prerequisites:** .NET 9 · Azure CLI · PowerShell 7 · (Node 20+ for the web UI) · `a365` CLI and
`ExchangeOnlineManagement` for live provisioning — `harness setup` checks/installs these.

---

## Status

| Capability | State |
|---|---|
| Build, run, UI, blueprint + agent-identity minting | ✅ works (Agent ID APIs are **beta**) |
| Agent registration / publish | ⚠️ needs **Agent Registry Administrator** + tenant **preview enrollment** |
| Purview enforcement end-to-end | ⚠️ needs the AI-app DLP policy + an identity with `Content.Process.*` (else regex fallback blocks) |
| Purview `processContent` code | ✅ matches the official v1.0 schema |

## Security

- Secrets and tenant config are git-ignored: `state/`, `**/a365.generated.config*.json`, `.env`,
  `**/a365.config.json`, `tenant-state.yaml`. No real tenant identifiers are committed (templates use placeholders).
- Rotate any blueprint client secret that was ever displayed; keep secrets in `.env` / a key vault, never in `appsettings.json`.

## References

[Agent 365](https://learn.microsoft.com/microsoft-agent-365/) ·
[`a365` CLI](https://learn.microsoft.com/microsoft-agent-365/developer/reference/cli/) ·
[Entra Agent ID](https://learn.microsoft.com/entra/agent-id/) ·
[Graph `processContent`](https://learn.microsoft.com/graph/api/userdatasecurityandgovernance-processcontent?view=graph-rest-1.0) ·
[DLP for AI apps](https://learn.microsoft.com/powershell/module/exchangepowershell/new-dlpcompliancepolicy)

## License

Add a license before publishing (e.g. MIT). Reference/demo code, as-is; the Microsoft preview APIs it targets may change.
