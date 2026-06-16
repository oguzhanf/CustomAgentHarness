# AI Use Policy — PSL-007

**Version:** 1.2  **Effective:** 2026-03-01  **Owner:** Technology Risk

## 1. Scope
Any AI system used to support AgenticBank business processes, whether built
in-house, procured, or accessed via a third-party API.

## 2. Tiered Governance
| Tier | Examples | Governance |
|---|---|---|
| T1 — Productivity assistance | Drafting emails, summarising meetings | Standard data-handling rules |
| T2 — Customer-facing advisory | Product recommendations, chat | Bias testing, log retention 5y, human override required |
| T3 — Decisioning | Credit scoring, fraud rules | Model risk committee approval, explainability artefacts, annual revalidation |

## 3. Mandatory Controls (all tiers)
- Identity: every agent must run under an enterprise-managed identity
  (e.g. Entra Agent ID) — no shared service accounts
- Observability: full OpenTelemetry tracing of inputs, outputs, tool calls
- Data protection: inputs and outputs must transit Purview classification
- Approval: every AI tool must have a named owner and sponsor

## 4. Prohibited Use
- Autonomous decisioning on credit decline / fraud reimbursement
- Generation of legal contracts without lawyer review
- Customer impersonation
---
*FICTITIOUS — sample workshop content for AgenticBank. Do not use in production.*

