#requires -Version 7.0
<#
.SYNOPSIS
  Ensures the operator (default: admin@example.org) holds the Microsoft Entra roles
  required to provision + register Agent 365 / Entra Agent ID agents, and to author
  Microsoft Purview DLP policies.

.DESCRIPTION
  Idempotent. Reuses your existing `az login` session (no interactive prompt) via
  `az rest` against Microsoft Graph. For each required role it:
    1. checks whether the principal already has an active role assignment, and
    2. if not, creates one (roleManagement/directory/roleAssignments).

  Roles ensured (Entra built-in, by roleTemplateId):
    - Agent Registry Administrator  6b942400-691f-4bf0-9d12-d8a254a2baf5
        Required for `copilot/agentRegistrations` (the `a365 publish` / register step).
    - Agent ID Administrator        db506228-d27e-4b7d-95e5-295956d6615f
    - Agent ID Developer            adb2368d-a9be-41b5-8667-d96778e081b0
        Create blueprints / mint agent identities (`a365 setup blueprint`).
    - Compliance Administrator      17315797-102d-40b4-93e0-432062caca18  (optional, -IncludePurview)
        Author Purview DLP policies (Global Administrator already covers this).

.NOTES
  * Assigning directory roles requires the caller to be Global Administrator or
    Privileged Role Administrator, and the az token to carry
    RoleManagement.ReadWrite.Directory. If your `az` session lacks that scope, run
    `az login --scope https://graph.microsoft.com/.default` first, or assign the
    roles in the Entra admin center (the script prints what is missing either way).
  * ❗ ROLE GRANTS ALONE DO NOT FIX THE AGENT-REGISTRATION 403. That endpoint also
    requires the TENANT to be enrolled in the Agent 365 / Entra Agent ID preview
    program. See the note printed at the end.

.PARAMETER UserPrincipalName
  The operator to grant roles to. Defaults to admin@example.org.

.PARAMETER IncludePurview
  Also ensure the Compliance Administrator role (for Purview DLP authoring).

.PARAMETER WhatIfMode
  Print what would change without making any assignment.

.EXAMPLE
  pwsh ./grant-agent-roles.ps1
.EXAMPLE
  pwsh ./grant-agent-roles.ps1 -UserPrincipalName admin@contoso.com -IncludePurview
#>
[CmdletBinding()]
param(
    [string]$UserPrincipalName = "",
    [switch]$IncludePurview,
    [switch]$WhatIfMode
)

$ErrorActionPreference = "Stop"

function Write-Step($m) { Write-Host ""; Write-Host "=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Write-Err($m)  { Write-Host "  [ERR]  $m" -ForegroundColor Red }

function Invoke-Graph {
    param([string]$Method, [string]$Uri, [string]$Body)
    $args = @("rest", "--method", $Method, "--uri", $Uri, "--headers", "Content-Type=application/json")
    if ($Body) { $args += @("--body", $Body) }
    $out = & az @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($out | Out-String) }
    if ($out) { return ($out | Out-String | ConvertFrom-Json) }
    return $null
}

$roles = [ordered]@{
    "Agent Registry Administrator" = "6b942400-691f-4bf0-9d12-d8a254a2baf5"
    "Agent ID Administrator"       = "db506228-d27e-4b7d-95e5-295956d6615f"
    "Agent ID Developer"           = "adb2368d-a9be-41b5-8667-d96778e081b0"
}
if ($IncludePurview) {
    $roles["Compliance Administrator"] = "17315797-102d-40b4-93e0-432062caca18"
}

Write-Step "Resolve principal: $UserPrincipalName"
if (-not $UserPrincipalName) {
    # Default to whoever is signed in to az — so this works in any tenant without editing.
    $UserPrincipalName = (az ad signed-in-user show --query userPrincipalName -o tsv 2>$null)
    if (-not $UserPrincipalName) { throw "No -UserPrincipalName given and could not read the signed-in az user. Run 'az login' or pass -UserPrincipalName." }
    Write-Ok "Using signed-in az user: $UserPrincipalName"
}
$user = Invoke-Graph GET "https://graph.microsoft.com/v1.0/users/$UserPrincipalName`?`$select=id,displayName,userPrincipalName"
$principalId = $user.id
Write-Ok "$($user.displayName) ($($user.userPrincipalName)) -> $principalId"

Write-Step "Current directory-role assignments"
$existing = Invoke-Graph GET "https://graph.microsoft.com/v1.0/roleManagement/directory/roleAssignments?`$filter=principalId eq '$principalId'"
$have = @($existing.value | ForEach-Object { $_.roleDefinitionId })
Write-Ok "Principal currently has $($have.Count) directory-role assignment(s)"

foreach ($name in $roles.Keys) {
    $roleId = $roles[$name]
    Write-Step "Role: $name ($roleId)"
    if ($have -contains $roleId) {
        Write-Ok "Already assigned - skipping"
        continue
    }
    if ($WhatIfMode) {
        Write-Warn2 "WHATIF - would assign '$name' to $UserPrincipalName"
        continue
    }
    $body = @{
        principalId      = $principalId
        roleDefinitionId = $roleId   # roleDefinitionId accepts the built-in roleTemplateId
        directoryScopeId = "/"
    } | ConvertTo-Json -Compress
    try {
        Invoke-Graph POST "https://graph.microsoft.com/v1.0/roleManagement/directory/roleAssignments" $body | Out-Null
        Write-Ok "Assigned '$name'"
    } catch {
        $msg = ($_.Exception.Message -split "`n")[0]
        if ($msg -match "already exist|conflict") {
            Write-Ok "Already assigned (conflict) - ok"
        } else {
            Write-Err "Could not assign '$name': $msg"
            Write-Warn2 "Assign it manually in the Entra admin center, or re-run after:"
            Write-Warn2 "  az login --scope https://graph.microsoft.com/.default"
        }
    }
}

Write-Step "Done - preview-enrollment reminder"
Write-Host @"

Roles ensure you CAN create blueprints, mint agent identities, author DLP policies,
and (with Agent Registry Administrator) call copilot/agentRegistrations.

HOWEVER: the 'a365 publish' / agent-registration step (Graph
'/beta/copilot/agentRegistrations') ALSO requires your TENANT to be enrolled in the
Agent 365 / Entra Agent ID preview program. If you still get a 403 'UnknownError'
after these roles are in place, the tenant is not enrolled - that is a Microsoft-side
onboarding step, not something a role grant can fix. Request enrollment via your
Microsoft contact / the Agent 365 onboarding form, then re-run:

  a365 setup all --agent-name ForgedAgentOne --authmode obo
"@ -ForegroundColor Gray
