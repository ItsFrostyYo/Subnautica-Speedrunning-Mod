param(
    [string] $GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018",
    [string] $TransportRoot = "C:\Program Files (x86)\Steam\steamapps\common\Subnautica",
    [switch] $BuildFirst
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot = Join-Path $root "dist\SubnauticaSpeedrunningRanked"
$launcherProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Launcher\SubnauticaSpeedrunningRanked.Launcher.csproj"
$bootstrapProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Bootstrap\SubnauticaSpeedrunningRanked.Bootstrap.csproj"
$runtimeProject = Join-Path $root "src\SubnauticaSpeedrunningRanked.Runtime\SubnauticaSpeedrunningRanked.Runtime.csproj"
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
        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
}

function Write-MissingOptionalFile {
    param([string] $Path)

    Write-Warning "Optional file not found: $Path"
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

Ensure-Directory $distRoot
Ensure-Directory (Join-Path $distRoot "Bootstrap")
Ensure-Directory (Join-Path $distRoot "Runtime")
Ensure-Directory (Join-Path $distRoot "Config")
Ensure-Directory (Join-Path $distRoot "Logs")
Ensure-Directory (Join-Path $distRoot "Modules")
Ensure-Directory (Join-Path $distRoot "Cache")
Ensure-Directory (Join-Path $distRoot "Data")

Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $launcherProject, "-c", "Release", "-o", $distRoot)
Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $bootstrapProject, "-c", "Release", "-o", (Join-Path $distRoot "Bootstrap"))
Invoke-NativeCommand -FilePath dotnet -Arguments @("publish", $runtimeProject, "-c", "Release", "-o", (Join-Path $distRoot "Runtime"))

$targetRankedRoot = Join-Path $GameRoot "SubnauticaSpeedrunningRanked"
Ensure-Directory $targetRankedRoot

Get-ChildItem -LiteralPath $distRoot -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $targetRankedRoot -Recurse -Force
}

foreach ($file in $directLauncherFiles) {
    $source = Join-Path $distRoot $file
    $destination = Join-Path $GameRoot $file
    Copy-IfExists -Source $source -Destination $destination
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

Write-Host "Published ranked loader to: $targetRankedRoot"
Write-Host "Native transport source checked at: $TransportRoot"
Write-Host "Run Launch Ranked.exe from the game root, or SubnauticaSpeedrunningRanked\\Launch Ranked.exe."
