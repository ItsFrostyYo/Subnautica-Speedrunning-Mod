param(
    [string] $ReleaseVersion = "",
    [string] $TransportRoot = "",
    [switch] $BuildFirst,
    [switch] $IncludeDebugSymbols
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcherRoot = Join-Path $root "Launcher"
$installerRoot = Join-Path $root "Installer"
$distRoot = Join-Path $launcherRoot "dist\SubnauticaSpeedrunningMod"
$releaseRoot = Join-Path $root "release"
$versionSourcePath = Join-Path $launcherRoot "src\Shared\ModClientRelease.cs"
$installerPublishPath = Join-Path $installerRoot "dist\SubnauticaSpeedrunningModInstaller.exe"

function Get-ClientReleaseVersion {
    param([string] $Path)

    $content = Get-Content -Raw -Path $Path
    $match = [regex]::Match($content, 'DisplayVersion\s*=\s*"([^"]+)"')
    if (-not $match.Success) {
        throw "Could not determine client version from $Path"
    }

    return $match.Groups[1].Value
}

if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
    $ReleaseVersion = Get-ClientReleaseVersion -Path $versionSourcePath
}

$ReleaseName = "SubnauticaSpeedrunningMod-" + $ReleaseVersion
$packageRoot = Join-Path $releaseRoot $ReleaseName
$archivePath = Join-Path $releaseRoot ($ReleaseName + ".zip")
$manifestPath = Join-Path $releaseRoot "latest.json"

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
& (Join-Path $installerRoot "build.ps1")

if (Test-Path $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

if (-not $IncludeDebugSymbols) {
    Get-ChildItem -LiteralPath $packageRoot -Filter *.pdb -Recurse -File -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

$installNotes = @"
Subnautica Speedrunning Mod Release
=====================================

1. Copy everything in this folder into the same folder as `Subnautica.exe`.
2. Make sure Subnautica is fully closed first.
3. Launch with:
   - Subnautica.exe
   - SubnauticaSpeedrunningMod\Launch Mod.exe

Update flow:
- Replace existing mod files with the new release contents.
- Keep the folder structure exactly the same.

Example game root:
C:\Program Files (x86)\Steam\steamapps\common\Subnautica
"@

Set-Content -Path (Join-Path $packageRoot "INSTALL.txt") -Value $installNotes -Encoding UTF8
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $archivePath -Force
Copy-Item -LiteralPath $installerPublishPath -Destination (Join-Path $releaseRoot "SubnauticaSpeedrunningModInstaller.exe") -Force

$manifest = @"
{
  "version": "$ReleaseVersion",
  "zipFileName": "$ReleaseName.zip",
  "zipUrl": ""
}
"@

Set-Content -Path $manifestPath -Value $manifest -Encoding UTF8

Write-Host "Release package created at: $packageRoot"
Write-Host "Release archive created at: $archivePath"
Write-Host "Release manifest created at: $manifestPath"
