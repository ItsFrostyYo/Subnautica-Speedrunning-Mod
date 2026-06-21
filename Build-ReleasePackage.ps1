param(
    [string] $ReleaseName = ("SubnauticaSpeedrunningRanked-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $TransportRoot = "C:\Program Files (x86)\Steam\steamapps\common\Subnautica",
    [switch] $BuildFirst,
    [switch] $IncludeDebugSymbols
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcherRoot = Join-Path $root "Launcher"
$distRoot = Join-Path $launcherRoot "dist\SubnauticaSpeedrunningRanked"
$releaseRoot = Join-Path $root "release"
$packageRoot = Join-Path $releaseRoot $ReleaseName
$archivePath = Join-Path $releaseRoot ($ReleaseName + ".zip")
$directLauncherFiles = @(
    "Launch Ranked.exe",
    "Launch Ranked.dll",
    "Launch Ranked.deps.json",
    "Launch Ranked.runtimeconfig.json"
)

function Ensure-Directory {
    param([string] $Path)

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Copy-IfExists {
    param(
        [string] $Source,
        [string] $Destination
    )

    if (Test-Path $Source) {
        Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
    }
}

if ($BuildFirst) {
    & (Join-Path $launcherRoot "build.ps1")
}

Ensure-Directory $releaseRoot
if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

& (Join-Path $launcherRoot "publish-to-game.ps1") -GameRoot $packageRoot -TransportRoot $TransportRoot

if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

if (-not $IncludeDebugSymbols) {
    Get-ChildItem -LiteralPath $packageRoot -Filter *.pdb -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

$installNotes = @"
Subnautica Speedrunning Ranked Release
=====================================

1. Copy everything in this folder into your Subnautica2018 game folder.
2. Make sure Subnautica is fully closed first.
3. Launch with:
   - Launch Ranked.exe
   - or SubnauticaSpeedrunningRanked\Launch Ranked.exe

Update flow:
- Replace existing ranked files with the new release contents.
- Keep the folder structure exactly the same.

Expected game root example:
C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018
"@

Set-Content -Path (Join-Path $packageRoot "INSTALL.txt") -Value $installNotes -Encoding UTF8
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $archivePath -Force

Write-Host "Release package created at: $packageRoot"
Write-Host "Release archive created at: $archivePath"
