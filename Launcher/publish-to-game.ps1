param(
    [string] $GameRoot = "",
    [string] $TransportRoot = "",
    [switch] $BuildFirst
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot = Join-Path $root "dist\SubnauticaSpeedrunningMod"
$launcherProject = Join-Path $root "src\SubnauticaSpeedrunningMod.Launcher\SubnauticaSpeedrunningMod.Launcher.csproj"
$bootstrapProject = Join-Path $root "src\SubnauticaSpeedrunningMod.Bootstrap\SubnauticaSpeedrunningMod.Bootstrap.csproj"
$runtimeProject = Join-Path $root "src\SubnauticaSpeedrunningMod.Runtime\SubnauticaSpeedrunningMod.Runtime.csproj"
$updaterProject = Join-Path $root "src\SubnauticaSpeedrunningMod.Updater\SubnauticaSpeedrunningMod.Updater.csproj"
$sharedSeedHelperScript = Join-Path $root "queue-shared-seed.ps1"
$legacyRootLauncherFiles = @(
    "Launch Ranked.exe",
    "Launch Ranked.dll",
    "Launch Ranked.deps.json",
    "Launch Ranked.runtimeconfig.json",
    "Launch Mod.exe",
    "Launch Mod.dll",
    "Launch Mod.deps.json",
    "Launch Mod.runtimeconfig.json"
)

function Ensure-Directory {
    param([string] $Path)

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Resolve-DefaultGameRoot {
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018",
        "C:\Program Files (x86)\Steam\steamapps\common\Subnautica"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "Subnautica.exe")) {
            return $candidate
        }
    }

    return $candidates[0]
}

function Resolve-DefaultTransportRoot {
    param([string] $ResolvedGameRoot)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($ResolvedGameRoot)) {
        $candidates += $ResolvedGameRoot
    }

    $candidates += @(
        "C:\Program Files (x86)\Steam\steamapps\common\Subnautica",
        "C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018"
    )

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if ((Test-Path (Join-Path $candidate "winhttp.dll")) -and (Test-Path (Join-Path $candidate ".doorstop_version"))) {
            return $candidate
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ResolvedGameRoot)) {
        return $ResolvedGameRoot
    }

    return "C:\Program Files (x86)\Steam\steamapps\common\Subnautica"
}

function Copy-IfExists {
    param(
        [string] $Source,
        [string] $Destination
    )

    if (Test-Path $Source) {
        $sourcePath = [System.IO.Path]::GetFullPath($Source)
        $destinationPath = [System.IO.Path]::GetFullPath($Destination)
        if ([string]::Equals($sourcePath, $destinationPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }

        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
}

function Write-MissingOptionalFile {
    param([string] $Path)

    Write-Warning "Optional file not found: $Path"
}

function Write-DoorstopConfig {
    param([string] $GameRoot)

    $configPath = Join-Path $GameRoot "doorstop_config.ini"
    $content = @"
# Managed by Subnautica Speedrunning Mod Launcher
[General]
enabled = true
target_assembly=SubnauticaSpeedrunningMod\Bootstrap\SubnauticaSpeedrunningMod.Bootstrap.dll
redirect_output_log = false
boot_config_override =
ignore_disable_switch = false

[UnityMono]
dll_search_path_override =
debug_enabled = false
debug_address = 127.0.0.1:10000
debug_suspend = false
"@

    Set-Content -Path $configPath -Value $content -Encoding ASCII
}

function Remove-IfExists {
    param([string] $Path)

    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

if ($BuildFirst) {
    & (Join-Path $root "build.ps1")
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Resolve-DefaultGameRoot
}

if ([string]::IsNullOrWhiteSpace($TransportRoot)) {
    $TransportRoot = Resolve-DefaultTransportRoot -ResolvedGameRoot $GameRoot
}

Ensure-Directory $distRoot
Ensure-Directory (Join-Path $distRoot "Bootstrap")
Ensure-Directory (Join-Path $distRoot "Runtime")
Ensure-Directory (Join-Path $distRoot "Config")
Ensure-Directory (Join-Path $distRoot "Logs")
Ensure-Directory (Join-Path $distRoot "Modules")
Ensure-Directory (Join-Path $distRoot "Cache")
Ensure-Directory (Join-Path $distRoot "Data")
Ensure-Directory (Join-Path $distRoot "Updater")

Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $launcherProject, "-c", "Release", "-o", $distRoot)
Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $bootstrapProject, "-c", "Release", "-o", (Join-Path $distRoot "Bootstrap"))
Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $runtimeProject, "-c", "Release", "-o", (Join-Path $distRoot "Runtime"))
Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $updaterProject, "-c", "Release", "-o", (Join-Path $distRoot "Updater"))
Copy-IfExists -Source $sharedSeedHelperScript -Destination (Join-Path $distRoot "queue-shared-seed.ps1")

$legacyInstallRoot = Join-Path $GameRoot "SubnauticaSpeedrunningRanked"
$targetModRoot = Join-Path $GameRoot "SubnauticaSpeedrunningMod"
if (Test-Path $legacyInstallRoot) {
    Remove-Item -LiteralPath $legacyInstallRoot -Recurse -Force
}

Ensure-Directory $targetModRoot

Get-ChildItem -LiteralPath $distRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $targetModRoot -Recurse -Force
}

foreach ($file in $legacyRootLauncherFiles) {
    Remove-IfExists -Path (Join-Path $GameRoot $file)
}

$transportFiles = @("winhttp.dll", ".doorstop_version")
foreach ($file in $transportFiles) {
    $source = Join-Path $TransportRoot $file
    $destination = Join-Path $GameRoot $file
    if (Test-Path $source) {
        Copy-IfExists -Source $source -Destination $destination
    }
    else {
        Write-MissingOptionalFile -Path $source
    }
}

Write-DoorstopConfig -GameRoot $GameRoot

Remove-IfExists -Path (Join-Path $GameRoot "Launch Ranked.cmd")
Remove-IfExists -Path (Join-Path $GameRoot "Launch Ranked.lnk")
Remove-IfExists -Path (Join-Path $GameRoot "Launch Mod.cmd")
Remove-IfExists -Path (Join-Path $GameRoot "Launch Mod.lnk")

Write-Host "Published mod loader to: $targetModRoot"
Write-Host "Native transport source checked at: $TransportRoot"
Write-Host "Run Subnautica.exe or SubnauticaSpeedrunningMod\\Launch Mod.exe after install."
