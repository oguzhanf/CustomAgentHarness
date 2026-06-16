# Demo script — AgenticBank Agent 365 workshop

Use this in front of you while you drive the TUI and browser. Each chapter contains:
1. **Setup** — what to have ready
2. **Talk track** — what you say (bullets)
3. **Action** — what to press / click
4. **Portal verify** — where to take the audience
5. **What to point out** — the "ahas"

Run timing: **~85 minutes** of demo, **8 chapters** in the TUI mapped to **5 narrative chapters** here.

---

## 0. Open

> **Talk:** "AgenticBank already has a custom agent harness. Tonight we're going to keep that harness and stitch it into Microsoft Agent 365 — which gives us governance, observability, identity, and content protection without rewriting the harness. Our reference harness here is called **YourCustomAgentHarness**; the blueprints, identities, and policies you'll see are real and were minted in the `example.org` tenant earlier."

> **Action:** In a clean terminal, double-check the harness is up:

```powershell
cd C:\customagentharness
.\harness.cmd status
```

> **Show:** 5 services green, ports listening, tenant info row. Then:

```powershell
.\harness.cmd demo
```

---

## Chapter 1 — Harness brings up agents

**TUI step 1 — "Status check"**

> **Talk:**
> - "First — our custom harness is already running. This is the part AgenticBank built."
> - "It runs the agent processes, the admin API, the web hub, and a local MCP server hosting our bank policy KB."
> - "**This is the workshop's #1 takeaway**: Agent 365 does NOT replace your harness. It plugs into it."

> **Action:** Press `Enter` to advance. TUI re-runs `harness status` in-window.

> **Point out:** The 5 services. Their ports. PIDs. "All local."

**TUI step 2 — "Load blueprint manifest"**

> **Talk:**
> - "Every agent has a manifest. This is a **policy artifact**, not just config — it declares identity, owner, sponsor, permissions, model, MCP tools, and content-protection rules."
> - "Here's the YAML. We wrote a thin wrapper around the a365 JSON format because the YAML reads better for governance reviews."

> **Action:** TUI prints the harness YAML in a syntax-highlighted panel. Then it runs the validator (`a365 develop validate`).

> **Point out:** `ownership.owner = admin@example.org`, `ownership.sponsor = admin@example.org`, `permissions.delegated = [Mail.Read, Calendars.Read, ...]`. "This is what your security team approves once and then the developer can't change without re-approval."

---

## Chapter 2 — Blueprint provisioning (Entra Agent ID)

**TUI step 3 — "Register blueprint with Entra"**

> **Talk:**
> - "Now we register this blueprint in Entra Agent ID. That mints two Entra objects: an **Agent ID Blueprint** (the policy) and an **Agent Identity** (the runnable principal)."
> - "Both have object IDs that Conditional Access, Purview, Defender, and Audit can target."

> **Action:** TUI runs `a365 setup blueprint --agent-name ForgedAgentOne --authmode obo --skip-requirements` (or shows the scripted view if you press `s`). Output includes the new Entra app IDs.

> **Portal verify (open in browser):**
> 1. **Entra → Agent ID Blueprints** (`https://entra.microsoft.com/#view/Microsoft_AAD_IAM/AgentIdentityBlueprintsBlade`) — show the new blueprint.
> 2. **Entra → Agent Identities** — show the matching identity. Same app id ↔ object id link as a regular service principal.

> **Point out:**
> - The blueprint contains the **declared** permissions; the identity has the **granted** permissions.
> - Owner + sponsor are tracked as separate attributes for accountability.
> - The audience should picture this as "the agent's HR file."

**TUI step 4 — "Mint agent identity (object details)"**

> **Talk:**
> - "Look at the Entra identity in detail. This is what every other Microsoft surface (Purview, Defender, Sign-in logs, Conditional Access) keys off."

> **Action:** TUI runs `az ad sp show --id <appid> -o jsonc`.

> **Point out:** appId, objectId, signInAudience, appOwnerOrganizationId, tags `agent365`, `blueprint:forged-agent-one`.

---

## Chapter 3 — Model binding + SDK initialization

**TUI step 5 — "Discover Foundry deployments"**

> **Talk:**
> - "The agent needs a model. AgenticBank is hosting a Foundry account so we don't have to push data to OpenAI directly. Notice that `disableLocalAuth=true` is set — no API keys are issued. The agent will use **DefaultAzureCredential** (Entra)."

> **Action:** TUI runs `az cognitiveservices account deployment list -g your-resource-group -n your-foundry-account -o table`.

> **Show:** gpt-4.1, gpt-5.1, text-embedding-3-small.

> **Portal verify:** Open Foundry portal (`https://ai.azure.com/?wsid=...`) → Deployments tab. Highlight that you can swap or deprecate a deployment and the agent will follow without code changes (deployment name is in the blueprint).

**TUI step 6 — "Init Agent 365 SDK + agent boot"**

> **Talk:**
> - "Now we boot the agent process. It reads the blueprint, calls the Agent 365 SDK, and starts streaming OpenTelemetry traces to the harness Activity Console."
> - "ForgedAgentOne is **delegated/OBO** — it calls Graph as the signed-in user. ForgedScholarTwo is **application-permission** — it has its own scoped permissions and answers from a knowledge base."

> **Action:** Both agent processes were started by `harness up`. The TUI shows a tail of each agent's OTel log: blueprint loaded, AOAI client built with DefaultAzureCredential, MCP servers connected, ASP.NET listening.

> **Point out:** "No API keys anywhere in the box. The agent authenticates as its **Entra Agent Identity** to Foundry. To you in the security team — that means rotation, anomaly detection, and revocation are all centrally managed."

**TUI step 7 — "Wire Microsoft Graph tools (delegated)"**

> **Talk:**
> - "ForgedAgentOne's blueprint declares `Mail.Read, Calendars.Read, Files.Read.All, Sites.Read.All` as **delegated** permissions. When the end user signs in, the agent will exchange the user's token via On-Behalf-Of flow to call Graph."

> **Action:** TUI runs `a365 develop add-mcp-servers MicrosoftGraph,SharePoint` (scripted).

> **Point out:** Look at the JSON output — Graph and SharePoint became registered MCP tools. The agent can now choose tools at runtime; the user's token bounds what they can do.

---

## Chapter 4 — Admin center registration + Purview governance

**TUI step 8 — "Publish to admin.microsoft.com"**

> **Talk:**
> - "Now the M365 admin can see and govern this agent. We publish it."

> **Action:** TUI runs `a365 publish --agent-name ForgedAgentOne --use-blueprint --dry-run`. (Real publish was done off-stage; dry-run shows the manifest.)

> **Portal verify:** Open `https://admin.microsoft.com/#/copilot/agents` → find `ForgedAgentOne`.
>
> Point out:
> - Display name, description, owner, sponsor.
> - The "Identity" tab links to the Entra agent.
> - The "Permissions" tab lists declared scopes.
> - Audit log entries for create.
> - Imagine the admin disabling, archiving, or transferring ownership of this agent. This is your governance plane.

**TUI step 9 — "Open the verification tabs"**

> **Talk:**
> - "Let's verify the same agent is visible across every relevant portal."

> **Action:** TUI prints a numbered portal menu — pick `[1]` for Admin Center, `[2]` for Entra Blueprint, `[3]` for Entra Identity, `[4]` for Foundry, `[5]` for Purview.

> **Point out:** The same object id appears everywhere — that's the magic of Agent 365 as the **identity backbone**.

**TUI step 10 — "Purview SDK blocks sensitive content (both directions)"**

> **Talk:**
> - "Now the killer feature for a bank. Microsoft Purview SDK is wired into the agent. We classify every inbound message AND every outbound reply. If a Sensitive Information Type is detected, the agent blocks it."
> - "This is bidirectional: users can't smuggle sensitive content INTO the agent, and the agent can't leak sensitive content OUT. The same SIT catalog you already use in DLP applies here."

> **Action:** TUI sends 6 canned prompts:
> 1. "Tell me a joke about banking" → OK
> 2. "What's our KYC threshold for high-risk customers?" → OK (KB answer with citations)
> 3. "My SSN is 123-45-6789, please help" → **BLOCKED inbound** (USSocialSecurityNumber)
> 4. "Issue a wire to IBAN GB29NWBK60161331926819 for account AGB-12345-6789" → **BLOCKED inbound** (IBAN, InternalAccountNumber)
> 5. "What does this customer file look like? [paste blob with fake card 4111-1111-1111-1111]" → **BLOCKED inbound** (CreditCardNumber)
> 6. Internal test where the LLM was nudged to invent an account number → **BLOCKED outbound**

> **Portal verify:** Open Purview portal (`https://purview.microsoft.com`) → **Data Map** → **Sensitive Information Types** to show the SIT catalog. If the Purview Auto policy was set up tonight, also open **Activity Explorer** to show the audit trail of the blocked actions.

> **Point out:** "Notice the reason text in the agent's response always includes which SIT(s) matched. Your audit team gets that for free."

---

## Chapter 5 — End-user experience

**Switch to browser:** `http://localhost:4001/hub`

> **Talk:**
> - "Now let's flip to the end-user view. This is the AgenticBank custom hub UI. Your developers built it; agent runtime lives behind. End user signs in with their normal AgenticBank Microsoft account."

> **Action:** Click **Sign in with Microsoft**. Sign in as `admin@example.org`. (Pre-grant consent off-stage so the popup is instant.)

> **Show:** UPN appears in the top bar, agent picker is enabled.

**TUI step 11 — "OBO demo with ForgedAgentOne (delegated Graph)"**

> **Action:** In the hub, pick **ForgedAgentOne**. Type:
> - "Summarize my last 3 unread emails."
> - "What's on my calendar for tomorrow?"
> - "Find me the policy doc in my SharePoint sites about wire transfers."

> **Talk:**
> - "The hub acquires a delegated Graph token for the signed-in user. That token is forwarded to the agent."
> - "The agent uses **On-Behalf-Of (OBO) flow** to exchange that token for a Graph token of its own — with the user's permissions clipped by the blueprint's declared scopes."
> - "If the user doesn't have access to a mailbox, the agent doesn't have access either. **No privilege escalation possible.**"

> **Show:** Each reply includes a small trace footer showing the Graph calls made and the resource owner.

> **Point out for security:**
> - The Entra **Sign-in logs** will show this OBO exchange as a separate event from the user's own sign-in. Conditional Access policies on the agent identity apply here.

**TUI step 12 — "App-permission KB demo with ForgedScholarTwo"**

> **Action:** In the hub, switch agent to **ForgedScholarTwo**. Type:
> - "What's our KYC threshold for high-risk customers?"
> - "What does our PEP policy say about source-of-wealth evidence?"
> - "When do we trigger an EDD process?"

> **Talk:**
> - "ForgedScholarTwo runs with **application permissions** — its own scope, no user identity in the call. It connects to the local MCP server which hosts 16 AgenticBank policy markdown files."
> - "It uses Azure OpenAI **embeddings** for retrieval — those are also called via DefaultAzureCredential, no keys."

> **Show:** Each reply lists citations like `[01-kyc-policy] (score 0.68)` and the agent grounds its answer in the KB chunks.

> **Point out:**
> - Two completely different identity + permission patterns from the same harness.
> - One blueprint per pattern.
> - Same Purview, same audit, same Defender — different identity behavior.

---

## Chapter 6 — Build your own agent (10 min walkthrough)

**Switch to browser:** `http://localhost:4001/admin/new`

> **Talk:**
> - "Last 10 minutes. How does a developer create a third agent definition?"

> **Action:** Walk through the wizard:
> 1. Name + description + tagline
> 2. Auth mode (OBO vs app vs both)
> 3. Permissions picker (search Graph permission names)
> 4. MCP tool picker
> 5. Model picker (auto-discovers Foundry deployments)
> 6. Purview SIT list
> 7. Review screen with the generated YAML
> 8. "Submit for approval" button — does NOT auto-publish; it opens a PR-style approval flow.

> **Point out:**
> - The developer can never bypass blueprint approval. Pull-request gates apply.
> - The same wizard's output is just the `.harness.yaml` file you already saw.

---

## Wrap (10 min)

> **Talk:**
> - "What you saw was a real Entra blueprint, real Entra agent identity, real Foundry-hosted model called via DefaultAzureCredential, real Purview SDK enforcement, real OBO with Graph, and real KB retrieval via MCP. All running on this laptop."
> - "The harness is yours. Agent 365 is the governance + identity + observability mesh that lets you ship agents that your CISO, your data privacy office, and your auditors can sign off on."
> - "Repo and leave-behind in your inbox by morning."

> **Q&A.**
