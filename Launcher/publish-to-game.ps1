param(
    [string] $GameRoot = "",
    [string] $TransportRoot = "",
    [switch] $BuildFirst,
    [switch] $CreateShortcut
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot = Join-Path $root "dist\SubnauticaSpeedrunningRanked"
$launcherProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Launcher\SubnauticaSpeedrunningRanked.Launcher.csproj"
$bootstrapProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Bootstrap\SubnauticaSpeedrunningRanked.Bootstrap.csproj"
$runtimeProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Runtime\SubnauticaSpeedrunningRanked.Runtime.csproj"
$updaterProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Updater\SubnauticaSpeedrunningRanked.Updater.csproj"
$sharedSeedHelperScript = Join-Path $root "queue-shared-seed.ps1"
$legacyRootLauncherFiles = @(
    "Launch Ranked.exe",
    "Launch Ranked.dll",
    "Launch Ranked.deps.json",
    "Launch Ranked.runtimeconfig.json"
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

function Remove-IfExists {
    param([string] $Path)

    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
}

function New-LauncherShortcut {
    param(
        [string] $ShortcutPath,
        [string] $TargetPath,
        [string] $WorkingDirectory
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "Launch Subnautica Speedrunning Ranked"
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
}

function Write-PortableRootLauncher {
    param([string] $Path)

    $content = @"
@echo off
setlocal
cd /d "%~dp0"
start "" "%~dp0SubnauticaSpeedrunningRanked\Launch Ranked.exe" %*
"@

    Set-Content -Path $Path -Value $content -Encoding ASCII
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

$targetRankedRoot = Join-Path $GameRoot "SubnauticaSpeedrunningRanked"
Ensure-Directory $targetRankedRoot

Get-ChildItem -LiteralPath $distRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $targetRankedRoot -Recurse -Force
}

foreach ($file in $legacyRootLauncherFiles) {
    Remove-IfExists -Path (Join-Path $GameRoot $file)
}

Write-PortableRootLauncher -Path (Join-Path $GameRoot "Launch Ranked.cmd")

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

$shortcutPath = Join-Path $GameRoot "Launch Ranked.lnk"
if ($CreateShortcut) {
    $targetPath = Join-Path $targetRankedRoot "Launch Ranked.exe"
    if (Test-Path $targetPath) {
        New-LauncherShortcut -ShortcutPath $shortcutPath -TargetPath $targetPath -WorkingDirectory $targetRankedRoot
    }
}
else {
    Remove-IfExists -Path $shortcutPath
}

Write-Host "Published ranked loader to: $targetRankedRoot"
Write-Host "Native transport source checked at: $TransportRoot"
Write-Host "Run Launch Ranked.cmd or SubnauticaSpeedrunningRanked\\Launch Ranked.exe after install."
