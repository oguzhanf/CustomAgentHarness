# Purview DSPM-for-AI + DLP — PowerShell workaround

## Why this script exists

The Microsoft Purview portal's **DSPM for AI → Setup tasks → "Secure interactions
from enterprise AI apps" → Create policies** flow is currently unreliable for
Entra-registered apps. The portal commonly silently fails (button click returns
to the same task page with no error). The supported workaround is to use the
**Security & Compliance Center PowerShell module** to create the equivalent
artefacts directly.

Two separate policies are needed for the workshop demo:

| Policy                                                | What it does                                                                                             |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `Capture` policy (FeatureConfiguration)               | Sends agent prompts + responses into Purview Audit + Activity Explorer for visibility / eDiscovery       |
| `DLP rule` targeting the Entra agent app              | Triggers `policyActions[].action == "restrictAccess"` in the `processContent` Graph response → BLOCK     |

`PurviewContentProtection.cs` reads the second one. The first is needed only for
"after-the-fact" visibility in the Purview portal during Q&A.

## Prerequisites

- PowerShell 7+
- Modules: `ExchangeOnlineManagement` (>= 3.5.0)
- Account: **Compliance Administrator** or **Security Administrator** role
- Two Entra app IDs (after `a365 setup blueprint`):
  - `ForgedAgentOne` clientId (delegated/OBO agent)
  - `ForgedScholarTwo` clientId (app-perm agent)

## Step 1 — Install + connect

```powershell
# Run as your admin account
Install-Module -Name ExchangeOnlineManagement -Scope CurrentUser -Force
Import-Module ExchangeOnlineManagement
Connect-IPPSSession -UserPrincipalName admin@example.org
```

## Step 2 — Capture (FeatureConfiguration)

This is what the portal button is supposed to create. Doing it via PowerShell
sidesteps the portal hang.

```powershell
# New-FeatureConfiguration (FeatureScenario KnowYourData) is in PUBLIC PREVIEW.
# ScenarioConfig + Locations follow the documented "capture interactions" shape.
$scenarioConfig   = '{"Activities":["UploadText","DownloadText"],"EnforcementPlanes":["Application","CopilotExperiences"],"SensitiveTypeIds":["All"],"IsIngestionEnabled":true}'
$captureLocations = '[{"Workload":"Applications","Location":"*","Inclusions":[],"Exclusions":[]}]'

New-FeatureConfiguration `
    -Name "DSPM for AI - Capture interactions for enterprise AI apps" `
    -FeatureScenario "KnowYourData" `
    -Mode "Enable" `
    -ScenarioConfig $scenarioConfig `
    -Locations $captureLocations
```

## Step 3 — DLP policy + rule that BLOCKS our agents (AI-app scoped)

The agents' Entra apps must be the **Locations** of a DLP policy on the
**`Application` enforcement plane**, with a rule whose action is
`RestrictAccess => ExcludeContentProcessing = Block`. When
`PurviewContentProtection` calls `processContent`, Graph then returns
`policyActions[].action = "restrictAccess"` and our middleware blocks the
prompt/response.

> ❗ Do **not** use `-ExchangeLocation "All"` — that scopes the policy to
> Exchange email only and never fires on agent prompts. Use `-Locations`
> (the agent app IDs) + `-EnforcementPlanes @("Application")`.

```powershell
# Replace these with the values from `a365 setup blueprint` output
$forgedAgentOneAppId   = '<ForgedAgentOne clientId>'
$forgedScholarTwoAppId = '<ForgedScholarTwo clientId>'

# Locations JSON: one entry per agent app, all users in tenant.
$loc = @(
    @{ Workload = "Applications"; Location = $forgedAgentOneAppId;   Inclusions = @(@{ Type = "Tenant"; Identity = "All" }) }
    @{ Workload = "Applications"; Location = $forgedScholarTwoAppId; Inclusions = @(@{ Type = "Tenant"; Identity = "All" }) }
) | ConvertTo-Json -Depth 6 -Compress

# Create the DLP policy on the Application (enterprise-AI-app) enforcement plane.
$policy = New-DlpCompliancePolicy `
    -Name "AgenticBank - Agent prompt protection" `
    -Comment "Blocks bank-sensitive content into and out of CustomAgentHarness AI agents" `
    -Locations $loc `
    -EnforcementPlanes @("Application") `
    -Mode "Enable"

# The rule: SIT conditions + the AI-app block action.
New-DlpComplianceRule `
    -Name "Block sensitive content for ForgedAgent*" `
    -Policy $policy.Name `
    -ContentContainsSensitiveInformation @(
        @{ Name = "Credit Card Number";                          minCount = "1" }
        @{ Name = "International Banking Account Number (IBAN)";  minCount = "1" }
        @{ Name = "SWIFT Code";                                  minCount = "1" }
        @{ Name = "U.S. Social Security Number (SSN)";           minCount = "1" }
    ) `
    -RestrictAccess @(@{ setting = "ExcludeContentProcessing"; value = "Block" }) `
    -NotifyUser @("LastModifier") `
    -GenerateAlert @("admin@example.org")
```

> The repo ships this as the runnable, idempotent
> `workshop/scripts/create-purview-policies.ps1` (supports `-WhatIfMode`).


## Step 4 — Verify

After ~5–10 minutes for replication, exercise the agent:

```bash
curl -X POST http://localhost:3979/chat \
     -H "Content-Type: application/json" \
     -d '{"message":"Customer SSN 120-98-1437 — please verify the account.","userObjectId":"22222222-2222-2222-2222-222222222222"}'
```

Expected response: `{"blocked": true, "direction":"user-to-agent", "reason":"Purview policy ..."}`.
In Purview Activity Explorer the request will appear with `Action = restrictAccess`.

## Reverting

```powershell
Get-DlpCompliancePolicy -Identity "AgenticBank — Agent prompt protection" | Remove-DlpCompliancePolicy -Confirm:$false
Get-FeatureConfiguration -Identity "DSPM for AI - Capture interactions for enterprise AI apps" | Remove-FeatureConfiguration -Confirm:$false
```

## Fallback

If both PowerShell paths fail (uncommon — usually the issue is RBAC),
`PurviewContentProtection.Mode = Auto` already falls back to the regex
classifier. The demo still blocks bank-sensitive content; it just won't
have a Purview audit trail.
