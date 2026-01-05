<#
.SYNOPSIS
    Deploys MSIX package for testing by unpacking and registering the manifest.

.DESCRIPTION
    This script finds the MSIX package (from downloaded artifacts or local build),
    unpacks it, and registers the AppxManifest.xml for testing purposes.
    It handles cleanup of previous installations and ensures makeappx.exe is available.

.PARAMETER MsixSearchPath
    The path to search for MSIX files. Can be a directory from downloaded artifacts or local build.

.PARAMETER IsWorkflowRun
    Boolean indicating if this is running as part of a workflow_run event.

.EXAMPLE
    .\deploy-msix.ps1 -MsixSearchPath ".\DownloadedMSIX" -IsWorkflowRun $true
    .\deploy-msix.ps1 -MsixSearchPath ".\AIDevGallery\AppPackages" -IsWorkflowRun $false
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$MsixSearchPath,
    
    [Parameter(Mandatory=$false)]
    [bool]$IsWorkflowRun = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== MSIX Deployment Script ==="
Write-Host "Search Path: $MsixSearchPath"
Write-Host "Is Workflow Run: $IsWorkflowRun"
Write-Host ""

# Find MSIX file
Write-Host "Looking for MSIX file in: $MsixSearchPath"
$msixFile = Get-ChildItem -Path $MsixSearchPath -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $msixFile) { 
    Write-Error "MSIX file not found in path: $MsixSearchPath"
    exit 1 
}

Write-Host "Using MSIX file: $($msixFile.FullName)"
Write-Host ""

# Cleanup previous installations
Write-Host "Cleaning up previous installations..."
$cleanupFailed = $false

$existingPackage = Get-AppxPackage -Name "*AIDevGallery*"
if ($existingPackage) {
    try {
        $existingPackage | Remove-AppxPackage
        Write-Host "Removed AIDevGallery package(s)"
    } catch {
        Write-Warning "Failed to remove AIDevGallery package: $_"
        $cleanupFailed = $true
    }
}

$existingPackage2 = Get-AppxPackage -Name "*e7af07c0*"
if ($existingPackage2) {
    try {
        $existingPackage2 | Remove-AppxPackage
        Write-Host "Removed e7af07c0 package(s)"
    } catch {
        Write-Warning "Failed to remove e7af07c0 package: $_"
        $cleanupFailed = $true
    }
}

if ($cleanupFailed) {
    Write-Warning "Some cleanup operations failed. This may cause installation issues if conflicting packages remain."
}

Write-Host "Cleanup completed"
Write-Host ""

# Ensure makeappx.exe is available
Write-Host "Checking for makeappx.exe..."
if (-not (Get-Command "makeappx.exe" -ErrorAction SilentlyContinue)) {
    Write-Host "makeappx.exe not found in PATH. Searching in Windows Kits..."
    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    
    if (Test-Path $kitsRoot) {
        $latestSdk = Get-ChildItem -Path $kitsRoot -Directory | 
                     Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } | 
                     Sort-Object Name -Descending | 
                     Select-Object -First 1
        
        if ($latestSdk) {
            $sdkPath = Join-Path $latestSdk.FullName "x64"
            if (Test-Path $sdkPath) {
                Write-Host "Found SDK at $sdkPath"
                $env:Path = "$sdkPath;$env:Path"
            }
        }
    }
}

if (-not (Get-Command "makeappx.exe" -ErrorAction SilentlyContinue)) {
    Write-Error "makeappx.exe could not be found. Please ensure Windows SDK is installed."
    exit 1
}

Write-Host "makeappx.exe found"
Write-Host ""

# Unpack MSIX
$unpackedDir = Join-Path $PWD "AIDevGallery_Unpacked"
if (Test-Path $unpackedDir) { 
    Write-Host "Removing existing unpacked directory..."
    Remove-Item -Path $unpackedDir -Recurse -Force 
}

Write-Host "Unpacking MSIX to $unpackedDir..."
makeappx.exe unpack /p $msixFile.FullName /d $unpackedDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to unpack MSIX. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "MSIX unpacked successfully"
Write-Host ""

# Register Manifest
$manifestPath = Join-Path $unpackedDir "AppxManifest.xml"
if (-not (Test-Path $manifestPath)) {
    Write-Error "AppxManifest.xml not found at: $manifestPath"
    exit 1
}

Write-Host "Registering AppxManifest.xml from $manifestPath..."
Add-AppxPackage -Register $manifestPath -ForceUpdateFromAnyVersion

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to register AppxPackage. Exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "=== MSIX package deployed successfully ==="
exit 0
