# MountainPet Windows Build Script
# Run from the MountainPet mod folder (inside Celeste/Mods/MountainPet/)
# Usage: Right-click -> Run with PowerShell, or: powershell -ExecutionPolicy Bypass -File build_windows.ps1

param(
    [switch]$Zip  # Pass -Zip to also create MountainPet.zip for distribution
)

$ErrorActionPreference = "Stop"

Write-Host "=== MountainPet Build Script ===" -ForegroundColor Cyan

# --- Check for .NET SDK ---
$dotnetVersion = $null
try {
    $dotnetVersion = & dotnet --version 2>$null
} catch {}

if (-not $dotnetVersion) {
    Write-Host ""
    Write-Host ".NET SDK not found. Installing..." -ForegroundColor Yellow
    Write-Host ""

    # Download and run the official .NET install script
    $installScript = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript

    # Install .NET 8 SDK (user-local, no admin needed)
    & $installScript -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\dotnet"

    # Add to PATH for this session
    $env:PATH = "$env:LOCALAPPDATA\dotnet;$env:PATH"
    $env:DOTNET_ROOT = "$env:LOCALAPPDATA\dotnet"

    # Verify
    $dotnetVersion = & dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Host "ERROR: Failed to install .NET SDK." -ForegroundColor Red
        Write-Host "Please install manually from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
        exit 1
    }
    Write-Host "Installed .NET SDK $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "Found .NET SDK $dotnetVersion" -ForegroundColor Green
}

# --- Verify we're in the right directory ---
$modRoot = $PSScriptRoot
if (-not $modRoot) { $modRoot = Get-Location }

$everestYaml = Join-Path $modRoot "everest.yaml"
$sourceDir = Join-Path $modRoot "Source"
$csproj = Join-Path $sourceDir "MountainPet.csproj"

if (-not (Test-Path $everestYaml)) {
    Write-Host "ERROR: everest.yaml not found. Run this script from the MountainPet mod folder." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $csproj)) {
    Write-Host "ERROR: Source/MountainPet.csproj not found." -ForegroundColor Red
    exit 1
}

# --- Check that Celeste.dll is reachable (3 levels up from Source/) ---
$celesteDll = Join-Path $sourceDir "..\..\..\Celeste.dll"
$celesteDll = [System.IO.Path]::GetFullPath($celesteDll)
if (-not (Test-Path $celesteDll)) {
    Write-Host ""
    Write-Host "WARNING: Celeste.dll not found at: $celesteDll" -ForegroundColor Yellow
    Write-Host "This mod must be inside Celeste/Mods/MountainPet/ to build." -ForegroundColor Yellow
    Write-Host "Expected structure: Celeste/Mods/MountainPet/Source/MountainPet.csproj" -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Try building anyway? (y/n)"
    if ($continue -ne "y") { exit 1 }
}

# --- Build ---
Write-Host ""
Write-Host "Building..." -ForegroundColor Cyan
Push-Location $sourceDir
try {
    & dotnet build -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Build succeeded!" -ForegroundColor Green

# --- Optional: Create zip for distribution ---
if ($Zip) {
    Write-Host ""
    Write-Host "Creating MountainPet.zip..." -ForegroundColor Cyan

    $zipPath = Join-Path $modRoot "MountainPet.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }

    # Build in Release mode (triggers the PackageMod target)
    Push-Location $sourceDir
    try {
        & dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Release build failed" -ForegroundColor Red
            exit 1
        }
    } finally {
        Pop-Location
    }

    if (Test-Path $zipPath) {
        $size = (Get-Item $zipPath).Length / 1KB
        Write-Host "Created: $zipPath ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
    } else {
        Write-Host "Zip not created (PackageMod target may need Release config)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Done. Restart Celeste to apply. ===" -ForegroundColor Cyan
Write-Host ""
