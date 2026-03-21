# RimMind Dev - Auto-update from GitHub
# Double-click this script or right-click > Run with PowerShell
#
# What it does:
# 1. Downloads the latest dev build from GitHub
# 2. Installs it into your RimWorld mods folder
# 3. Optionally launches the game

param(
    [string]$Branch = "dev"
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  RimMind Dev - Auto-update from GitHub" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# --- Detect RimWorld install path ---
$steamPaths = @(
    "C:\Program Files (x86)\Steam",
    "C:\Program Files\Steam",
    "D:\Steam",
    "D:\SteamLibrary",
    "E:\Steam",
    "E:\SteamLibrary"
)

$rimworldPath = $null
foreach ($sp in $steamPaths) {
    $candidate = Join-Path $sp "steamapps\common\RimWorld"
    if (Test-Path $candidate) {
        $rimworldPath = $candidate
        break
    }
}

# Also check Steam's libraryfolders.vdf for custom library paths
if (-not $rimworldPath) {
    $vdfPath = "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $vdfPath) {
        $vdfContent = Get-Content $vdfPath -Raw
        $paths = [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
        foreach ($p in $paths) {
            $candidate = Join-Path $p "steamapps\common\RimWorld"
            if (Test-Path $candidate) {
                $rimworldPath = $candidate
                break
            }
        }
    }
}

if (-not $rimworldPath) {
    Write-Host "ERROR: Could not find RimWorld installation!" -ForegroundColor Red
    Write-Host "Please edit this script and set the path manually." -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

$modsPath = Join-Path $rimworldPath "Mods"
$modFolder = Join-Path $modsPath "RimMind"
Write-Host "RimWorld found at: $rimworldPath" -ForegroundColor Green
Write-Host "Mod folder: $modFolder" -ForegroundColor Gray
Write-Host ""

# --- Close RimWorld if running ---
$rimProcess = Get-Process -Name "RimWorldWin64" -ErrorAction SilentlyContinue
if ($rimProcess) {
    Write-Host "Closing RimWorld..." -ForegroundColor Yellow
    Stop-Process -Name "RimWorldWin64" -Force
    Start-Sleep -Seconds 3
}

# --- Download latest from GitHub ---
$repo = "Bwarhness/RimMind"
$zipUrl = "https://github.com/$repo/archive/refs/heads/$Branch.zip"
$tempZip = Join-Path $env:TEMP "RimMind-$Branch.zip"
$tempExtract = Join-Path $env:TEMP "RimMind-extract"

Write-Host "Downloading latest $Branch branch..." -ForegroundColor Cyan
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $zipUrl -OutFile $tempZip -UseBasicParsing
} catch {
    Write-Host "ERROR: Failed to download from GitHub!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host "Downloaded." -ForegroundColor Green

# --- Extract ---
Write-Host "Extracting..." -ForegroundColor Cyan
if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

# GitHub zips have a top-level folder like "RimMind-dev"
$extracted = Get-ChildItem $tempExtract | Select-Object -First 1

# --- Build the DLL ---
Write-Host "Building mod..." -ForegroundColor Cyan
$hasDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($hasDotnet) {
    Push-Location $extracted.FullName
    try {
        & dotnet restore Source/RimMind/RimMind.csproj 2>&1 | Out-Null
        & dotnet build Source/RimMind/RimMind.csproj -c Release --no-restore 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARNING: dotnet build failed. Trying to use pre-built DLL..." -ForegroundColor Yellow
        } else {
            Write-Host "Build succeeded." -ForegroundColor Green
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "dotnet SDK not found - checking for pre-built DLL..." -ForegroundColor Yellow
}

$dllPath = Join-Path $extracted.FullName "Assemblies\RimMind.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: No RimMind.dll found! You need the .NET SDK installed." -ForegroundColor Red
    Write-Host "Install from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# --- Install to mods folder ---
Write-Host "Installing to RimWorld mods folder..." -ForegroundColor Cyan

# Backup existing mod settings if present
$settingsBackup = $null
$settingsFile = Join-Path $modFolder ".claude"
if (Test-Path $modFolder) {
    # Remove old mod files but keep local config
    $keepItems = @(".git", ".claude")
    Get-ChildItem $modFolder -Force | Where-Object { $_.Name -notin $keepItems } | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $modFolder -Force | Out-Null
}

# Copy new files (skip git/dev stuff)
$skipItems = @(".git", ".github", ".gitignore", ".claude", "Source", "tools", "Logs")
Get-ChildItem $extracted.FullName -Force | Where-Object { $_.Name -notin $skipItems } | ForEach-Object {
    Copy-Item $_.FullName -Destination $modFolder -Recurse -Force
}

# Make sure Assemblies folder exists and has the DLL
$assemblyDir = Join-Path $modFolder "Assemblies"
if (-not (Test-Path $assemblyDir)) { New-Item -ItemType Directory -Path $assemblyDir -Force | Out-Null }
Copy-Item $dllPath -Destination $assemblyDir -Force

Write-Host "Installed successfully!" -ForegroundColor Green

# --- Cleanup ---
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

# --- Version info ---
$aboutXml = Join-Path $modFolder "About\About.xml"
if (Test-Path $aboutXml) {
    $version = ([xml](Get-Content $aboutXml)).ModMetaData.modVersion
    Write-Host ""
    Write-Host "Installed RimMind v$version ($Branch)" -ForegroundColor Green
}

# --- Launch game ---
Write-Host ""
$launch = Read-Host "Launch RimWorld? (Y/n)"
if ($launch -ne "n" -and $launch -ne "N") {
    Write-Host "Launching RimWorld..." -ForegroundColor Cyan
    Start-Process "steam://run/294100"
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Read-Host "Press Enter to close"
