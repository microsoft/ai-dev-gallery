<#
.SYNOPSIS
    Creates a local nuget.config for development, picking the right template based
    on whether the machine can reach the public nuget.org feed.

.DESCRIPTION
    Some managed devices (e.g. corporate networks with restricted egress) block
    direct TLS access to api.nuget.org and instead require routing package
    restore through an internal proxy feed. This script probes nuget.org and
    copies nuget.config.template (public) if it's reachable, or
    nuget.config.managed.template (proxy) if it's not, so a fresh clone builds
    successfully regardless of the network the developer is on.

    Both templates map the same *Foundry* pattern to the ORT feed, which is
    required regardless of network: it's the only feed hosting the exact
    Microsoft.ML.OnnxRuntime.Foundry version needed by
    Microsoft.AI.Foundry.Local.Core.WinML (nuget.org only has a differently
    versioned build, which triggers a NU1603 warning that fails the build
    given this repo's TreatWarningsAsErrors=true setting).

    nuget.config is gitignored (see .gitignore) and only ever generated locally;
    it is never committed. If nuget.config already exists, this script does
    nothing, so any manual override a developer has made is preserved.

.PARAMETER RepoRoot
    Root of the repository containing nuget.config.template / nuget.config.managed.template.
    Defaults to the directory this script lives in, two levels up.
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
)

$configPath = Join-Path $RepoRoot "nuget.config"
$publicTemplatePath = Join-Path $RepoRoot "nuget.config.template"
$managedTemplatePath = Join-Path $RepoRoot "nuget.config.managed.template"

if (Test-Path $configPath) {
    # Respect any existing local config (manual override or already generated).
    exit 0
}

if (-not (Test-Path $publicTemplatePath)) {
    Write-Warning "nuget.config.template not found at $publicTemplatePath; skipping nuget.config generation."
    exit 0
}

$publicNuGetReachable = $false
try {
    Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -UseBasicParsing -TimeoutSec 5 | Out-Null
    $publicNuGetReachable = $true
} catch {
    $publicNuGetReachable = $false
}

if ($publicNuGetReachable) {
    Copy-Item -Path $publicTemplatePath -Destination $configPath
    Write-Host "Created nuget.config from nuget.config.template (nuget.org reachable)."
} elseif (Test-Path $managedTemplatePath) {
    Copy-Item -Path $managedTemplatePath -Destination $configPath
    Write-Host "Created nuget.config from nuget.config.managed.template (nuget.org unreachable; using internal proxy feed)."
} else {
    # No proxy template available; fall back to the public template so restore
    # at least attempts nuget.org directly and surfaces a clear network error.
    Copy-Item -Path $publicTemplatePath -Destination $configPath
    Write-Warning "nuget.org unreachable and nuget.config.managed.template not found; falling back to nuget.config.template."
}
