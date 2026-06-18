#Requires -Version 5.1
<#
.SYNOPSIS
  One-command bootstrap for YourCustomAgentHarness.

.DESCRIPTION
  Centralizes EVERYTHING needed to go from a fresh Windows machine to a running harness:

    1. Ensures PowerShell 7 (installs it if missing) and RE-LAUNCHES itself under pwsh 7.
    2. Installs every prerequisite that is missing (via winget):
         git, .NET 10 SDK, Azure CLI, Node.js, PowerShell 7
       plus the Agent 365 CLI (dotnet tool) and the ExchangeOnlineManagement module.
    3. Builds the solution.
    4. Runs `harness setup` (tenant: az login, .env, Entra roles, provisioning, Purview).

  Idempotent and safe to re-run — anything already present is skipped.

.PARAMETER Yes
  Non-interactive: pass --yes through to `harness setup`.

.PARAMETER SkipInstall
  Skip prerequisite installation (assume tools are present).

.PARAMETER SkipBuild
  Skip `dotnet build`.

.PARAMETER SkipProvision
  Install + build only; do NOT run the `harness setup` tenant flow.

.PARAMETER DotnetChannel
  .NET SDK channel used by the dotnet-install fallback. Default: 10.0.

.EXAMPLE
  ./setup.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\setup.ps1 -SkipProvision
#>
[CmdletBinding()]
param(
    [switch]$Yes,
    [switch]$SkipInstall,
    [switch]$SkipBuild,
    [switch]$SkipProvision,
    [string]$DotnetChannel = "10.0"
)

# ============================================================================
#  0. PowerShell 7 prerequisite + self-relaunch  (this block must stay 5.1-safe)
# ============================================================================
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "[setup] PowerShell 7 is required for the harness. Checking..." -ForegroundColor Cyan
    $pwshPath = $null
    $c = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($c) { $pwshPath = $c.Source }

    if (-not $pwshPath) {
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Host "[setup] Installing PowerShell 7 via winget..." -ForegroundColor Cyan
            winget install --id Microsoft.PowerShell -e --silent --accept-source-agreements --accept-package-agreements
        } else {
            Write-Host "[setup] winget not found. Install PowerShell 7 from https://aka.ms/powershell and re-run." -ForegroundColor Red
            exit 1
        }
        # refresh PATH from the registry so the freshly-installed pwsh is visible
        $env:PATH = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
        $c = Get-Command pwsh -ErrorAction SilentlyContinue
        if ($c) { $pwshPath = $c.Source }
        if (-not $pwshPath) {
            $guess = Join-Path $env:ProgramFiles 'PowerShell\7\pwsh.exe'
            if (Test-Path $guess) { $pwshPath = $guess }
        }
    }

    if (-not $pwshPath) {
        Write-Host "[setup] PowerShell 7 was installed but could not be located. Open a NEW terminal and re-run setup.ps1." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "[setup] Relaunching under PowerShell 7 ($pwshPath)..." -ForegroundColor Cyan
    $relaunch = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File', $PSCommandPath)
    if ($Yes)          { $relaunch += '-Yes' }
    if ($SkipInstall)  { $relaunch += '-SkipInstall' }
    if ($SkipBuild)    { $relaunch += '-SkipBuild' }
    if ($SkipProvision){ $relaunch += '-SkipProvision' }
    $relaunch += @('-DotnetChannel', $DotnetChannel)
    & $pwshPath @relaunch
    exit $LASTEXITCODE
}

# ============================================================================
#  From here on we are guaranteed to be running under PowerShell 7.
# ============================================================================
$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot

function Write-Step($m) { Write-Host ""; Write-Host "=== $m ===" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn2($m){ Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Write-Err($m)  { Write-Host "  [ERR]  $m" -ForegroundColor Red }

function Have([string]$name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

function Update-SessionPath {
    $machine = [Environment]::GetEnvironmentVariable('Path','Machine')
    $user    = [Environment]::GetEnvironmentVariable('Path','User')
    $env:PATH = @($machine, $user, $env:PATH) -join ';'
}

function Add-SessionPath([string]$dir) {
    if ($dir -and (Test-Path $dir) -and (-not ($env:PATH.Split(';') -contains $dir))) {
        $env:PATH = "$dir;$env:PATH"
    }
}

function Install-WithWinget([string]$id, [string]$name) {
    Write-Step "Installing $name ($id)"
    try {
        winget install --id $id -e --silent --accept-source-agreements --accept-package-agreements --disable-interactivity | Out-Host
    } catch {
        Write-Warn2 "winget reported: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
    }
    Update-SessionPath
}

function Confirm-Winget {
    if (Have 'winget') { return }
    Write-Err "winget (App Installer) is required to auto-install prerequisites."
    Write-Err "Install 'App Installer' from the Microsoft Store, OR install these manually and re-run with -SkipInstall:"
    Write-Err "  git, .NET 10 SDK, Azure CLI, Node 20+, PowerShell 7"
    throw "winget not found"
}

function Confirm-Tool([string]$cmd, [string]$wingetId, [string]$name) {
    if (Have $cmd) { Write-Ok "$name already installed"; return }
    Install-WithWinget $wingetId $name
    if (Have $cmd) { Write-Ok "$name installed" }
    else { Write-Warn2 "$name installed but not on PATH in this session; a new terminal may be required." }
}

function Confirm-Dotnet10 {
    $ok = $false
    if (Have 'dotnet') {
        $sdks = & dotnet --list-sdks 2>$null
        if ($sdks -match '^10\.') { $ok = $true }
    }
    if ($ok) { Write-Ok ".NET 10 SDK already installed"; return }

    Install-WithWinget 'Microsoft.DotNet.SDK.10' '.NET 10 SDK'
    Add-SessionPath (Join-Path $env:ProgramFiles 'dotnet')

    $have10 = $false
    if (Have 'dotnet') { if ((& dotnet --list-sdks 2>$null) -match '^10\.') { $have10 = $true } }
    if (-not $have10) {
        Write-Step "Falling back to the official dotnet-install script (channel $DotnetChannel)"
        $installer = Join-Path $env:TEMP 'dotnet-install.ps1'
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
        & $installer -Channel $DotnetChannel -InstallDir (Join-Path $env:ProgramFiles 'dotnet')
        Add-SessionPath (Join-Path $env:ProgramFiles 'dotnet')
    }

    $have10 = $false
    if (Have 'dotnet') { if ((& dotnet --list-sdks 2>$null) -match '^10\.') { $have10 = $true } }
    if ($have10) { Write-Ok ".NET 10 SDK installed" }
    else { Write-Err ".NET 10 SDK not detected. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and re-run." }
}

function Confirm-A365 {
    Add-SessionPath (Join-Path $env:USERPROFILE '.dotnet\tools')
    if (Have 'a365') { Write-Ok "Agent 365 CLI already installed"; return }
    Write-Step "Installing the Agent 365 CLI (dotnet tool)"
    & dotnet tool install --global Microsoft.Agents.A365.DevTools.Cli | Out-Host
    Add-SessionPath (Join-Path $env:USERPROFILE '.dotnet\tools')
    if (Have 'a365') { Write-Ok "Agent 365 CLI installed" }
    else { Write-Warn2 "a365 installed to ~/.dotnet/tools; ensure that folder is on PATH (a new terminal usually fixes it)." }
}

function Confirm-ExchangeModule {
    if (Get-Module -ListAvailable -Name ExchangeOnlineManagement) { Write-Ok "ExchangeOnlineManagement already installed"; return }
    Write-Step "Installing the ExchangeOnlineManagement module (CurrentUser)"
    try {
        Install-Module -Name ExchangeOnlineManagement -Scope CurrentUser -Force -AllowClobber
        Write-Ok "ExchangeOnlineManagement installed"
    } catch {
        Write-Warn2 "Could not install ExchangeOnlineManagement: $($_.Exception.Message.Split([Environment]::NewLine)[0])"
        Write-Warn2 "It is only needed for the Purview DLP step; you can install it later."
    }
}

# ── banner ──
Write-Host ""
Write-Host "  YourCustomAgentHarness - setup" -ForegroundColor DarkYellow
Write-Host "  PowerShell $($PSVersionTable.PSVersion) - $root" -ForegroundColor DarkGray

# ── 1. prerequisites ──
if (-not $SkipInstall) {
    Confirm-Winget
    Confirm-Tool 'git' 'Git.Git' 'Git'
    Confirm-Dotnet10
    Confirm-Tool 'az'   'Microsoft.AzureCLI'   'Azure CLI'
    Confirm-Tool 'node' 'OpenJS.NodeJS.LTS'    'Node.js'
    Confirm-Tool 'pwsh' 'Microsoft.PowerShell' 'PowerShell 7'
    Confirm-A365
    Confirm-ExchangeModule
} else {
    Write-Step "Skipping prerequisite installation (-SkipInstall)"
}

# ── 2. build ──
if (-not $SkipBuild) {
    Write-Step "Building the solution"
    & dotnet build (Join-Path $root 'YourCustomAgentHarness.sln') --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { Write-Err "build failed"; exit 1 }
    Write-Ok "build succeeded"
} else {
    Write-Step "Skipping build (-SkipBuild)"
}

# ── 3. tenant setup (delegates to the harness TUI) ──
if (-not $SkipProvision) {
    Write-Step "Running harness setup (az login, .env, Entra roles, provisioning, Purview)"
    $tui = Join-Path $root 'apps\harness.tui\harness.tui.csproj'
    $setupArgs = @('run','--project', $tui, '--', 'setup')
    if ($Yes) { $setupArgs += '--yes' }
    & dotnet @setupArgs
} else {
    Write-Host ""
    Write-Ok "Prerequisites + build done. Next, run the tenant setup:"
    Write-Host "    dotnet run --project apps/harness.tui -- setup" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Done. Start everything with:  dotnet run --project apps/harness.tui -- up" -ForegroundColor Green
