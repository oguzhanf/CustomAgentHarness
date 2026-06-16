# AgenticBank ↔ Microsoft Agent 365 Workshop

**Audience:** Architects, developers, systems engineers, security engineers
**Duration:** 3 hours
**Outcome:** Every IT discipline leaves knowing how to onboard a custom-built agent onto Agent 365 — with identity, governance, content protection, observability, and end-user delivery patterns.

---

## Pre-flight (presenter, ≤10 min before start)

```powershell
cd C:\customagentharness
.\harness.cmd doctor           # 5 green probes (az, a365, foundry, aoai-auth, purview)
.\harness.cmd up               # 5 services listening (4000, 4001, 3979, 3980, 3981)
.\harness.cmd status           # confirm green
```

Open these tabs in a fresh browser profile signed in as `admin@example.org`:

1. https://admin.microsoft.com/#/copilot/agents
2. https://entra.microsoft.com/#view/Microsoft_AAD_IAM/AgentIdentityBlueprintsBlade
3. https://entra.microsoft.com/#view/Microsoft_AAD_IAM/AgentIdentitiesBlade
4. https://ai.azure.com/?wsid=/subscriptions/11111111-1111-1111-1111-111111111111
5. https://purview.microsoft.com/datamapsearch?feature.tenant=example.org
6. https://localhost:4001/admin (admin UI)
7. https://localhost:4001/hub (end-user UI)

---

## Schedule

| Time         | Block                                                          | Format       | Owner        |
|--------------|----------------------------------------------------------------|--------------|--------------|
| 0:00 – 0:10  | Welcome, room round-table, agenda                              | Slides       | Presenter    |
| 0:10 – 0:25  | Microsoft AI for the enterprise — where Agent 365 fits         | Slides       | Presenter    |
| 0:25 – 0:50  | Agent 365 concepts: blueprint, identity, observability, Purview, Defender, Work IQ | Slides + whiteboard | Presenter |
| 0:50 – 1:00  | "Where the AgenticBank harness fits" — your harness + the SDK  | Architecture diagram | Presenter |
| 1:00 – 1:10  | Demo introduction — what we will see                            | TUI banner   | Presenter    |
| 1:10 – 1:25  | **Chapter 1** — Harness brings up agents (Steps 1-2)            | TUI live     | Presenter    |
| 1:25 – 1:40  | **Chapter 2** — Blueprint provisioning (Steps 3-4)              | TUI + Entra  | Presenter    |
| 1:40 – 1:45  | ☕ Break                                                        |              |              |
| 1:45 – 2:00  | **Chapter 3** — Model binding + SDK init (Steps 5-7)            | TUI + Foundry| Presenter    |
| 2:00 – 2:15  | **Chapter 4** — Admin center registration + Purview (Steps 8-10)| TUI + admin.microsoft.com + Purview | Presenter |
| 2:15 – 2:35  | **Chapter 5** — End-user experience (Steps 11-12)               | Hub UI live  | Presenter + audience suggests prompts |
| 2:35 – 2:50  | "Build your own agent definition" walk-through                  | Admin UI     | Presenter    |
| 2:50 – 3:00  | Q&A, governance/security wrap, leave-behind handout             | Discussion   | Presenter    |

---

## Roles in the room

| Role | What they should pay attention to |
|---|---|
| Architects | The split between harness responsibilities (memory, knowledge, execution) and Agent 365 responsibilities (identity, governance, observability). Two reference templates: delegated/OBO + application-permission. |
| Developers | The .NET 8 + Semantic Kernel + Agent 365 SDK integration pattern. The blueprint manifest schema. Where MCP fits. |
| Systems | The local-host topology, the port map, the harness CLI (`up/down/status/doctor`), and how the TUI demo runs. |
| Security | Entra Agent ID, blueprint as policy artifact, Purview SDK enforcement (bidirectional), Defender posture (covered in slides), and `az` Entra-auth + DefaultAzureCredential (no API keys). |

---

## Materials handed out

- `workshop/leave-behind.md` (printable PDF) — one-pager exec + technical
- `workshop/architecture.excalidraw` — system topology + identity flow
- `workshop/demo-script.md` — full step-by-step demo script (this presenter copy only)
- GitHub link with the full harness source

---

## Recovery plan if something breaks

| Failure | Fallback (presenter does this without breaking flow) |
|---|---|
| Internet drops | TUI is local-only. Agents are local-only. Demo can continue. Skip portal-verification steps. |
| Foundry rate-limit / 429 | Presenter says "we're seeing a Foundry throttle, scripted view shows what the call returns" — press `s` for scripted in the fallback menu. |
| `a365` CLI fails | Press `s` for the fallback scripted output. Real Entra artifacts already exist from yesterday's run. |
| MSAL.js popup blocked | Open `chrome://settings/content/popups`, allow `localhost`, retry. |
| Purview SDK throws | The harness auto-degrades to the regex/SIT classifier; reason will say "Regex fallback (Purview unavailable)". Demo proceeds with full blocking still working. |
| Hub chat UI returns "unable to reach Azure OpenAI" | Means DefaultAzureCredential expired — open another terminal, run `az login --tenant 00000000-0000-0000-0000-000000000000`, retry. |

---

## Hand-out cheatsheet for the audience

```
Repo:        github.com/customer/customagentharness (this is the demo code)
Tenant:      example.org
TUI:         harness demo  (or harness up/down/status/doctor)
Hub:         http://localhost:4001/hub
Admin:       http://localhost:4001/admin
Blueprints:  /blueprints/forged-agent-one.harness.yaml
             /blueprints/forged-scholar-two.harness.yaml
Model:       Azure OpenAI (Foundry your-foundry-account → gpt-4.1)
Auth:        Entra (DefaultAzureCredential) — NO API keys
KB:          local MCP server at :3981 over 16 markdown policy docs
```
