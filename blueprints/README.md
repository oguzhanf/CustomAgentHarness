# Blueprints

This folder contains agent blueprint manifests for **YourCustomAgentHarness** — the customer's custom agent runtime.

Each agent has **two files**:

| File | Audience | Purpose |
|---|---|---|
| `*.harness.yaml` | The custom agent harness itself | Rich, human-friendly definition with hosting, retrieval, observability, content protection. This is what a harness operator authors. |
| `*.a365.json` | The Agent 365 CLI (`a365 setup blueprint`) | Trimmed, schema-conformant blueprint payload Microsoft expects. Generated from the `.harness.yaml` by the harness at provisioning time. |

The dual-file design is deliberate: it shows the customer **exactly how their existing harness format maps to Microsoft's**. No magic. No lock-in. Their authoring tooling continues to work; the harness takes care of the translation.

## Agents shipped in this demo

| Blueprint | Auth | Model | What it does |
|---|---|---|---|
| `forged-agent-one` | OBO (delegated) | gpt-4.1 | Calls Graph on behalf of the signed-in banker |
| `forged-scholar-two` | S2S (application) | gpt-4.1 | Answers from AgenticBank policy KB via local MCP |

## Workflow

```
.harness.yaml --(harness.tui transform)-->  .a365.json
                                                |
                                                v
                                       a365 setup blueprint
                                                |
                                                v
                                Entra Agent Blueprint + Agent ID
                                                |
                                                v
                                       a365 publish
                                                |
                                                v
                                  admin.microsoft.com agent
```
