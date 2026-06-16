#requires -Version 7.0
<#
.SYNOPSIS
  Provisions the Microsoft Purview DSPM-for-AI policies the AgenticBank
  workshop needs, working around the portal "Create policies" hang.

.DESCRIPTION
  Creates:
    1) DSPM-for-AI FeatureConfiguration so prompts/responses appear in
       Activity Explorer (the part the silently-failing portal button is
       supposed to create).
    2) A DLP rule that names the two ForgedAgent* Entra apps as targets
       and BLOCKS sensitive content. This is what makes our agents
       actually return "blocked" on Purview-protected prompts.

.NOTES
  Run after `a365 setup blueprint --agent-name ForgedAgentOne` and
  `a365 setup blueprint --agent-name ForgedScholarTwo` have completed so
  the agents' client IDs are available. The script prompts for them if
  they aren't already in tenant-state.yaml.

  Required role on admin@example.org: Compliance Administrator
                                    OR Security Administrator
                                    OR Global Administrator.

.PARAMETER ForgedAgentOneAppId
  The clientId/appId of the ForgedAgentOne Entra app (delegated/OBO agent).

.PARAMETER ForgedScholarTwoAppId
  The clientId/appId of the ForgedScholarTwo Entra app (app-perm agent).

.PARAMETER UserPrincipalName
  Admin account to connect with (defaults to admin@example.org).

.PARAMETER SkipCapture
  Skip the FeatureConfiguration (use if portal already created it).

.PARAMETER WhatIfMode
  Print everything that would be created without making changes.

.EXAMPLE
  pwsh ./create-purview-policies.ps1
  # interactive — prompts for both agent app IDs

.EXAMPLE
  pwsh ./create-purview-policies.ps1 -ForgedAgentOneAppId abc123 -ForgedScholarTwoAppId def456
#>

[CmdletBinding()]
param(
    [string]$ForgedAgentOneAppId,
    [string]$ForgedScholarTwoAppId,
    [string]$UserPrincipalName = "",
    [switch]$SkipCapture,
    [switch]$WhatIfMode
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) {
    Write-Host ""
    Write-Host "=== $msg ===" -ForegroundColor Cyan
}

function Write-Ok($msg)   { Write-Host "  [OK]   $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "  [ERR]  $msg" -ForegroundColor Red }

# 0. Resolve the admin UPN (defaults to the signed-in az user so this works in any tenant).
if (-not $UserPrincipalName) {
    $UserPrincipalName = (az ad signed-in-user show --query userPrincipalName -o tsv 2>$null)
    if (-not $UserPrincipalName) { $UserPrincipalName = Read-Host "Admin UPN to connect to Security & Compliance" }
}

# 1. Resolve agent app IDs (param > .env > tenant-state.yaml > prompt)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$stateFile = Join-Path $repoRoot "tenant-state.yaml"

# Load .env from the repo root so the same values drive PowerShell + the .NET apps.
$envFile = Join-Path $repoRoot ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | Where-Object { $_ -match '^\s*[^#].*?=' } | ForEach-Object {
        $k, $v = $_ -split '=', 2
        $k = $k.Trim(); $v = ($v -replace '\s+#.*$','').Trim().Trim('"').Trim("'")
        if ($k -and -not [Environment]::GetEnvironmentVariable($k)) { Set-Item -Path "Env:$k" -Value $v }
    }
}
if (-not $ForgedAgentOneAppId)   { $ForgedAgentOneAppId   = $env:FORGEDAGENTONE_APP_ID }
if (-not $ForgedScholarTwoAppId) { $ForgedScholarTwoAppId = $env:FORGEDSCHOLARTWO_APP_ID }
if ((-not $ForgedAgentOneAppId -or -not $ForgedScholarTwoAppId) -and (Test-Path $stateFile)) {
    $stateText = Get-Content $stateFile -Raw
    if (-not $ForgedAgentOneAppId) {
        $m = [regex]::Match($stateText, "displayName:\s*ForgedAgentOne[^a]+appId:\s*([\w-]+)")
        if ($m.Success) { $ForgedAgentOneAppId = $m.Groups[1].Value }
    }
    if (-not $ForgedScholarTwoAppId) {
        $m = [regex]::Match($stateText, "displayName:\s*ForgedScholarTwo[^a]+appId:\s*([\w-]+)")
        if ($m.Success) { $ForgedScholarTwoAppId = $m.Groups[1].Value }
    }
}
if (-not $ForgedAgentOneAppId) {
    $ForgedAgentOneAppId = Read-Host "ForgedAgentOne app (client) ID"
}
if (-not $ForgedScholarTwoAppId) {
    $ForgedScholarTwoAppId = Read-Host "ForgedScholarTwo app (client) ID"
}
Write-Ok "ForgedAgentOne  appId = $ForgedAgentOneAppId"
Write-Ok "ForgedScholarTwo appId = $ForgedScholarTwoAppId"

# 2. Ensure ExchangeOnlineManagement module is present
Write-Step "Ensure ExchangeOnlineManagement module"
$mod = Get-Module -ListAvailable -Name ExchangeOnlineManagement | Sort-Object Version -Descending | Select-Object -First 1
if (-not $mod) {
    Write-Warn2 "ExchangeOnlineManagement not installed - installing for current user"
    Install-Module -Name ExchangeOnlineManagement -Scope CurrentUser -Force -AllowClobber
    $mod = Get-Module -ListAvailable -Name ExchangeOnlineManagement | Sort-Object Version -Descending | Select-Object -First 1
}
Write-Ok "Module: ExchangeOnlineManagement $($mod.Version)"
Import-Module ExchangeOnlineManagement -Force

# 3. Connect to Security and Compliance PowerShell
Write-Step "Connect-IPPSSession (interactive sign-in) as $UserPrincipalName"
Connect-IPPSSession -UserPrincipalName $UserPrincipalName -ShowBanner:$false
Write-Ok "Connected"

# 4. Capture FeatureConfiguration (DSPM-for-AI visibility)
if (-not $SkipCapture) {
    Write-Step "FeatureConfiguration - Capture interactions for enterprise AI apps"
    $featureName = "DSPM for AI - Capture interactions for enterprise AI apps"
    $existing = $null
    try { $existing = Get-FeatureConfiguration -Identity $featureName -FeatureScenario "KnowYourData" -ErrorAction Stop } catch { }
    if ($existing) {
        Write-Warn2 "FeatureConfiguration already exists - leaving as-is"
    } else {
        # Documented ScenarioConfig shape for "Capture interactions for enterprise AI apps"
        # (New-FeatureConfiguration -FeatureScenario KnowYourData). This is the
        # collection policy that surfaces prompts/responses in DSPM-for-AI Activity Explorer.
        # NOTE: New-FeatureConfiguration is in PUBLIC PREVIEW and may not exist in all tenants.
        $scenarioConfig  = '{"Activities":["UploadText","DownloadText"],"EnforcementPlanes":["Application","CopilotExperiences"],"SensitiveTypeIds":["All"],"IsIngestionEnabled":true}'
        $captureLocations = '[{"Workload":"Applications","Location":"*","Inclusions":[],"Exclusions":[]}]'
        if ($WhatIfMode) {
            Write-Host "ScenarioConfig: $scenarioConfig" -ForegroundColor DarkGray
            Write-Host "Locations:      $captureLocations" -ForegroundColor DarkGray
            Write-Warn2 "WHATIF - would have created FeatureConfiguration"
        } else {
            # If the cmdlet/preview isn't available in this tenant, warn and continue.
            # This step controls Activity Explorer *visibility* only — it does NOT affect
            # the DLP blocking behavior below, which is what the demo depends on.
            try {
                New-FeatureConfiguration `
                    -Name $featureName `
                    -FeatureScenario "KnowYourData" `
                    -Mode "Enable" `
                    -ScenarioConfig $scenarioConfig `
                    -Locations $captureLocations `
                    -ErrorAction Stop | Out-Null
                Write-Ok "FeatureConfiguration created"
            } catch {
                Write-Warn2 "FeatureConfiguration skipped: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
                Write-Warn2 "This is non-fatal (preview cmdlet) — it only affects DSPM-for-AI Activity Explorer"
                Write-Warn2 "visibility, not the DLP blocking rule created below. Continuing."
            }
        }
    }
}

# 5. DLP policy + rule scoped to the AGENTS' Entra apps (AI-app enforcement plane)
#
# IMPORTANT (corrected 2026-06): a DLP policy that should govern AI agents must be
# scoped to the *Application* enforcement plane with the agent Entra app IDs as
# Locations — NOT `-ExchangeLocation All` (which only covers Exchange email and
# never fires on `processContent` / agent prompts). The block action for AI apps is
# `-RestrictAccess @(@{setting="ExcludeContentProcessing";value="Block"})`, which is
# what makes the Graph `processContent` response return
# policyActions[].action = "restrictAccess" that PurviewContentProtection.cs reads.
# Refs: New-DlpCompliancePolicy / New-DlpComplianceRule official docs ("Copilot / AI app" example).
Write-Step "DLP policy - AgenticBank Agent Prompt Protection (AI-app scoped)"
$policyName = "AgenticBank - Agent prompt protection"

# Locations JSON: one entry per agent Entra app, all users in tenant.
$locations = @(
    @{ Workload = "Applications"; Location = $ForgedAgentOneAppId;   Inclusions = @(@{ Type = "Tenant"; Identity = "All" }) }
    @{ Workload = "Applications"; Location = $ForgedScholarTwoAppId; Inclusions = @(@{ Type = "Tenant"; Identity = "All" }) }
)
$locationsJson = ConvertTo-Json $locations -Depth 6 -Compress

# Enforcement plane for custom, Entra-registered enterprise AI apps that call the
# Purview processContent Graph API. (Use "CopilotExperiences" instead to target
# Microsoft 365 Copilot, or "Browser"/"Network" for unmanaged-AI-app scenarios.)
$enforcementPlane = "Application"

$existingPolicy = $null
try { $existingPolicy = Get-DlpCompliancePolicy -Identity $policyName -ErrorAction Stop } catch { }
if ($existingPolicy) {
    Write-Warn2 "DLP policy already exists - reusing (locations not modified)"
    $policy = $existingPolicy
} elseif ($WhatIfMode) {
    Write-Host "Locations: $locationsJson" -ForegroundColor DarkGray
    Write-Warn2 "WHATIF - would create DLP policy '$policyName' on EnforcementPlanes=[$enforcementPlane]"
    $policy = [pscustomobject]@{ Name = $policyName }
} else {
    try {
        $policy = New-DlpCompliancePolicy `
            -Name $policyName `
            -Comment "Blocks bank-sensitive content into and out of CustomAgentHarness AI agents" `
            -Locations $locationsJson `
            -EnforcementPlanes @($enforcementPlane) `
            -Mode "Enable" -ErrorAction Stop
        Write-Ok "DLP policy created (EnforcementPlanes=[$enforcementPlane], 2 agent-app locations)"
    } catch {
        Write-Err "Failed to create AI-app-scoped DLP policy: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
        Write-Warn2 "Your tenant may not yet expose the 'Application' enforcement plane (DSPM-for-AI preview)."
        Write-Warn2 "The agents still block via the regex SIT fallback (Purview.Mode=Auto). Aborting policy step."
        throw
    }
}

Write-Step "DLP rule - Block sensitive content for ForgedAgent* (RestrictAccess/ExcludeContentProcessing)"
$ruleName = "Block sensitive content for ForgedAgent*"
$existingRule = $null
try { $existingRule = Get-DlpComplianceRule -Identity $ruleName -ErrorAction Stop } catch { }
if ($existingRule -and -not $WhatIfMode) {
    Write-Warn2 "DLP rule already exists - removing for clean re-create"
    Remove-DlpComplianceRule -Identity $ruleName -Confirm:$false | Out-Null
}

# Built-in Purview SIT display names (case-insensitive). Tune minCount as needed.
$sits = @(
    @{ Name = "Credit Card Number";                          minCount = "1" }
    @{ Name = "International Banking Account Number (IBAN)";  minCount = "1" }
    @{ Name = "SWIFT Code";                                  minCount = "1" }
    @{ Name = "U.S. Social Security Number (SSN)";           minCount = "1" }
)

# For AI-app DLP the block action is RestrictAccess => ExcludeContentProcessing=Block,
# which is surfaced to processContent as policyActions[].action = "restrictAccess".
$ruleParams = @{
    Name                                = $ruleName
    Policy                              = $policy.Name
    ContentContainsSensitiveInformation = $sits
    RestrictAccess                      = @(@{ setting = "ExcludeContentProcessing"; value = "Block" })
    NotifyUser                          = @("LastModifier")
    GenerateAlert                       = @($UserPrincipalName)
}

if ($WhatIfMode) {
    Write-Host ($ruleParams | ConvertTo-Json -Depth 8) -ForegroundColor DarkGray
    Write-Warn2 "WHATIF - would create DLP rule '$ruleName'"
} else {
    try {
        New-DlpComplianceRule @ruleParams -ErrorAction Stop | Out-Null
        Write-Ok "DLP rule created (RestrictAccess=ExcludeContentProcessing/Block on 4 SITs)"
    } catch {
        Write-Warn2 "RestrictAccess rule failed: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
        Write-Warn2 "Retrying with legacy -BlockAccess form..."
        try {
            New-DlpComplianceRule -Name $ruleName -Policy $policy.Name `
                -ContentContainsSensitiveInformation $sits `
                -BlockAccess $true -BlockAccessScope "All" `
                -NotifyUser @("LastModifier") -GenerateAlert @($UserPrincipalName) `
                -ErrorAction Stop | Out-Null
            Write-Ok "DLP rule created (legacy -BlockAccess form)"
        } catch {
            Write-Warn2 "Rule creation failed: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
            Write-Warn2 "Policy '$($policy.Name)' exists but is rule-less. Add a rule in purview.microsoft.com."
            Write-Warn2 "Agents continue to block via the regex SIT fallback (Purview.Mode=Auto)."
        }
    }
}

# 6. Verification hints
Write-Step "Done"
Write-Host @"

Verify by exercising an agent (after ~5 min for policy replication):

  curl.exe -X POST http://localhost:3979/chat \``
       -H "Content-Type: application/json" \``
       -d '{\"message\":\"Customer SSN 120-98-1437 - please verify\",\"userObjectId\":\"22222222-2222-2222-2222-222222222222\"}'

Expected: { "blocked": true, "direction": "user-to-agent", ... }

Then open Purview Activity Explorer:
  https://purview.microsoft.com/datasecurity/dspmforai

You should see the request with Action = restrictAccess.

To revert later:
  Get-DlpCompliancePolicy '$policyName' | Remove-DlpCompliancePolicy -Confirm:`$false
  Get-FeatureConfiguration -Identity 'DSPM for AI - Capture interactions for enterprise AI apps' | Remove-FeatureConfiguration -Confirm:`$false
"@
