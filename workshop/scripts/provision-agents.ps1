#requires -Version 7.0
<#
.SYNOPSIS
  Provisions (or re-provisions) the two demo agents into Microsoft Entra using the
  Agent 365 CLI (`a365`): creates each blueprint, mints its agent identity, configures
  inheritable + MCP permissions, and attempts agent registration / publish.

.DESCRIPTION
  Wraps the `a365` CLI so the blueprint + agent-identity minting is reproducible.
  Runs from each agent's publish folder (where its a365.config.json lives).

  Agents:
    - ForgedAgentOne   (authmode: obo  — delegated / on-behalf-of)
    - ForgedScholarTwo (authmode: s2s  — application / service-to-service)

  ❗ INTERACTIVE: the a365 CLI uses MSAL/WAM interactive sign-in (a browser/WAM
  dialog may appear). You must be signed in as an account with the Agent ID
  Developer / Agent ID Administrator roles (run ./grant-agent-roles.ps1 first).

  ❗ The final "agent registration / publish" step calls
  /beta/copilot/agentRegistrations, which additionally requires the tenant to be
  enrolled in the Agent 365 / Entra Agent ID preview program. A 403 there is a
  tenant-onboarding issue, not a script bug — the blueprint + identity are still
  created successfully.

.PARAMETER TenantId
  The Entra tenant id. Defaults to the value in ../../tenant-state.yaml.

.PARAMETER Agents
  Which agents to provision. Default: both.

.PARAMETER Publish
  Also run `a365 publish` after setup (creates the admin-center package).

.EXAMPLE
  pwsh ./provision-agents.ps1
.EXAMPLE
  pwsh ./provision-agents.ps1 -Agents ForgedScholarTwo   # finish the missing identity
#>
[CmdletBinding()]
param(
    [string]$TenantId,
    [ValidateSet("ForgedAgentOne", "ForgedScholarTwo")]
    [string[]]$Agents = @("ForgedAgentOne", "ForgedScholarTwo"),
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
function Write-Step($m) { Write-Host ""; Write-Host "=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "  [WARN] $m" -ForegroundColor Yellow }

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

# Resolve tenant id: -TenantId param > .env TENANT_ID > tenant-state.yaml
if (-not $TenantId) { $TenantId = $env:TENANT_ID }
if (-not $TenantId -and (Test-Path $stateFile)) {
    $m = [regex]::Match((Get-Content $stateFile -Raw), "(?m)^\s*id:\s*([0-9a-fA-F-]{36})")
    if ($m.Success) { $TenantId = $m.Groups[1].Value }
}
if (-not $TenantId) { throw "TenantId not provided (pass -TenantId, set TENANT_ID in .env, or fill tenant-state.yaml)." }
Write-Ok "Tenant: $TenantId"

# Resolve the a365 CLI
$a365 = @(
    (Join-Path $env:USERPROFILE ".dotnet\tools\a365.exe"),
    (Join-Path $env:USERPROFILE ".dotnet\tools\a365.cmd")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $a365) { $a365 = (Get-Command a365 -ErrorAction SilentlyContinue)?.Source }
if (-not $a365) {
    throw "a365 CLI not found. Install it: dotnet tool install --global Microsoft.Agents.A365.DevTools.Cli"
}
Write-Ok "a365: $a365"

# Resolve the owner/operator UPN from tenant-state.yaml so blueprints are owned by YOUR account
# (the templates ship with the placeholder admin@example.org).
$ownerUpn = $null
if ($env:ADMIN_UPN -and $env:ADMIN_UPN -ne 'admin@example.org') { $ownerUpn = $env:ADMIN_UPN }
if (-not $ownerUpn -and (Test-Path $stateFile)) {
    $om = [regex]::Match((Get-Content $stateFile -Raw), "(?m)^\s*admin:\s*(\S+@\S+)")
    if ($om.Success -and $om.Groups[1].Value -ne 'admin@example.org') { $ownerUpn = $om.Groups[1].Value }
}

$authMode      = @{ ForgedAgentOne = "obo"; ForgedScholarTwo = "s2s" }
$pubDir        = @{ ForgedAgentOne = "publish\forged-agent-one"; ForgedScholarTwo = "publish\forged-scholar-two" }
$blueprintBase = @{ ForgedAgentOne = "forged-agent-one"; ForgedScholarTwo = "forged-scholar-two" }

foreach ($agent in $Agents) {
    Write-Step "Provisioning $agent (authmode=$($authMode[$agent]))"

    # Provision the owner/sponsor UPN into this agent's blueprint files from tenant-state.yaml.
    if ($ownerUpn) {
        foreach ($ext in @("harness.yaml", "a365.json")) {
            $bpFile = Join-Path $repoRoot ("blueprints\{0}.{1}" -f $blueprintBase[$agent], $ext)
            if (Test-Path $bpFile) {
                (Get-Content $bpFile -Raw).Replace("admin@example.org", $ownerUpn) | Set-Content $bpFile -NoNewline -Encoding UTF8
            }
        }
        Write-Ok "Blueprint owner/sponsor set to $ownerUpn"
    } else {
        Write-Warn2 "Owner UPN not resolved from tenant-state.yaml (tenant.admin). Blueprint owner stays admin@example.org — set tenant.admin or edit the blueprint before going live."
    }

    $dir = Join-Path $repoRoot $pubDir[$agent]
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Push-Location $dir
    try {
        Write-Warn2 "Interactive sign-in may appear. Complete it to continue."
        & $a365 setup all --agent-name $agent --authmode $authMode[$agent] --tenant-id $TenantId --skip-requirements --verbose
        if ($LASTEXITCODE -ne 0) {
            Write-Warn2 "a365 setup exited with code $LASTEXITCODE (often the registration/publish 403 — blueprint + identity may still be created)."
        } else {
            Write-Ok "$agent setup completed"
        }
        if ($Publish) {
            & $a365 publish
            if ($LASTEXITCODE -ne 0) { Write-Warn2 "a365 publish exited $LASTEXITCODE (preview enrollment required for registration)." }
        }
    } finally {
        Pop-Location
    }
}

Write-Step "Verify minted identities (read-only, via Microsoft Graph)"
az rest --method GET --uri "https://graph.microsoft.com/beta/servicePrincipals?`$filter=servicePrincipalType eq 'ServiceIdentity'&`$select=displayName,appId" `
    --query "value[?contains(displayName,'Forged')].{name:displayName,appId:appId}" -o table 2>$null

Write-Host ""
Write-Ok "Done. Next: fill each agent's Entra app id into apps/<Agent>/appsettings.json (Purview:AppLocationValue),"
Write-Ok "then run workshop/scripts/create-purview-policies.ps1 to scope the DLP policy to those app ids."
