param(
    [string]$VersionLabel = "Beta-0.11.3",
    [string]$GameInstallRoot = "C:\Program Files (x86)\Steam\steamapps\common\SubnauticaRanked"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$launcherRoot = Join-Path $repoRoot "Launcher"
$srcRoot = Join-Path $launcherRoot "src"
$runtimeProject = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Runtime\SubnauticaSpeedrunningMod.Runtime.csproj"
$bootstrapProject = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Bootstrap\SubnauticaSpeedrunningMod.Bootstrap.csproj"
$launcherProject = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Launcher\SubnauticaSpeedrunningMod.Launcher.csproj"
$updaterProject = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Updater\SubnauticaSpeedrunningMod.Updater.csproj"
$bridgeProject = Join-Path $srcRoot "SubnauticaSpeedrunningMod.NetworkBridge\SubnauticaSpeedrunningMod.NetworkBridge.csproj"

$installModRoot = Join-Path $GameInstallRoot "SubnauticaSpeedrunningMod"
$releaseFolderName = "SubnauticaSpeedrunningMod-$VersionLabel"
$releaseRoot = Join-Path $PSScriptRoot $releaseFolderName
$zipPath = Join-Path $PSScriptRoot ($releaseFolderName + ".zip")
$latestJsonPath = Join-Path $PSScriptRoot "latest.json"
$buildTempRoot = Join-Path $PSScriptRoot ".build-temp"
$launcherPublishRoot = Join-Path $buildTempRoot "launcher"
$updaterPublishRoot = Join-Path $buildTempRoot "updater"
$bridgePublishRoot = Join-Path $buildTempRoot "bridge"

$practiceSourceRoot = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Runtime\PracticeSaveFiles"
$languageSourceRoot = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Runtime\LanguageFiles"
$ruleset7SeedPoolPath = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Runtime\Seeds\Ruleset7\1000SurvivalGlitchedAny%.csv"

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Reset-Directory {
    param([string]$Path)
    $resolved = [IO.Path]::GetFullPath($Path)
    $releaseBase = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
    $installBase = [IO.Path]::GetFullPath($installModRoot).TrimEnd('\') + '\'
    if (-not ($resolved.StartsWith($releaseBase, [StringComparison]::OrdinalIgnoreCase) -or
              $resolved.StartsWith($installBase, [StringComparison]::OrdinalIgnoreCase))) {
        throw "Refusing to reset a directory outside release/install output: $resolved"
    }
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination
    )

    Ensure-Directory -Path $Destination
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Remove-FilesByPattern {
    param(
        [string]$Root,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

function Write-LatestManifest {
    param(
        [string]$ManifestPath,
        [string]$Version,
        [string]$ZipFileName,
        [string]$Sha256
    )

    $manifest = @{
        version = $Version
        zipFileName = $ZipFileName
        zipUrl = "https://github.com/ItsFrostyYo/Subnautica-Speedrunning-Mod/releases/download/$Version/$ZipFileName"
        sha256 = $Sha256.ToUpperInvariant()
    }

    $manifest | ConvertTo-Json | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
}

function Test-ReleasePackage {
    param(
        [string]$PackagePath,
        [string]$ExpectedVersion,
        [string]$ExpectedSeedPoolPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $requiredEntries = @(
            ".doorstop_version",
            "doorstop_config.ini",
            "Launch Mod.cmd",
            "winhttp.dll",
            "SubnauticaSpeedrunningMod/Launch Mod.exe",
            "SubnauticaSpeedrunningMod/Bootstrap/Mono.Cecil.dll",
            "SubnauticaSpeedrunningMod/Bootstrap/SubnauticaSpeedrunningMod.Bootstrap.dll",
            "SubnauticaSpeedrunningMod/Runtime/SubnauticaSpeedrunningMod.Runtime.dll",
            "SubnauticaSpeedrunningMod/Updater/Mod Updater.exe",
            "SubnauticaSpeedrunningMod/Seeds/1000SurvivalGlitchedAny%.csv"
        )

        foreach ($requiredEntry in $requiredEntries) {
            if ($entries -notcontains $requiredEntry) {
                throw "Release package is missing required entry: $requiredEntry"
            }
        }

        $unexpectedBuildFiles = @($entries | Where-Object { $_ -match '(?i)\.(pdb|cs)$' })
        if ($unexpectedBuildFiles.Count -gt 0) {
            throw "Release package contains source/debug files: $($unexpectedBuildFiles -join ', ')"
        }
        if ($entries | Where-Object { $_ -match '(?i)(^|/)Temporary[^/]*Validation\.dll$' }) {
            throw 'Release package contains a temporary validation harness.'
        }
        $doorstop = $archive.Entries | Where-Object { $_.FullName -eq 'doorstop_config.ini' } | Select-Object -First 1
        $reader = [IO.StreamReader]::new($doorstop.Open())
        try { $doorstopText = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($doorstopText -notmatch 'target_assembly\s*=\s*SubnauticaSpeedrunningMod[\\/]Bootstrap[\\/]SubnauticaSpeedrunningMod\.Bootstrap\.dll') {
            throw 'Release package does not point to the normal mod bootstrap.'
        }
    }
    finally {
        $archive.Dispose()
    }

    $launcherPath = Join-Path $releaseRoot "SubnauticaSpeedrunningMod\Launch Mod.exe"
    $launcherVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($launcherPath).ProductVersion
    if (-not [string]::Equals($launcherVersion, $ExpectedVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Release launcher version '$launcherVersion' does not match '$ExpectedVersion'."
    }

    $seedCount = @(Get-Content -LiteralPath $ExpectedSeedPoolPath | Where-Object { $_ -match '^\s*[+-]?\d+\s*$' }).Count
    if ($seedCount -ne 1000) {
        throw "Ruleset 7 release seed pool contains $seedCount numeric seeds instead of 1000."
    }
}

Write-Host "Building Subnautica Speedrunning Mod release $VersionLabel..."

Reset-Directory -Path $buildTempRoot

dotnet build $runtimeProject -c Release | Out-Host
dotnet build $bootstrapProject -c Release | Out-Host
dotnet publish $launcherProject -c Release -o $launcherPublishRoot | Out-Host
dotnet publish $updaterProject -c Release -o $updaterPublishRoot | Out-Host
dotnet publish $bridgeProject -c Release -o $bridgePublishRoot | Out-Host

$runtimeOutputRoot = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Runtime\bin\Release\net35"
$bootstrapOutputRoot = Join-Path $srcRoot "SubnauticaSpeedrunningMod.Bootstrap\bin\Release\net35"

Write-Host "Updating local install at $GameInstallRoot..."

Ensure-Directory -Path $installModRoot
Ensure-Directory -Path (Join-Path $installModRoot "Runtime")
Ensure-Directory -Path (Join-Path $installModRoot "Bootstrap")
Ensure-Directory -Path (Join-Path $installModRoot "Bridge")
Ensure-Directory -Path (Join-Path $installModRoot "Updater")
Ensure-Directory -Path (Join-Path $installModRoot "LanguageFiles")
Ensure-Directory -Path (Join-Path $installModRoot "SaveFiles")
Ensure-Directory -Path (Join-Path $installModRoot "Seeds")

Copy-DirectoryContents -Source $launcherPublishRoot -Destination $installModRoot
Copy-Item -LiteralPath (Join-Path $bootstrapOutputRoot "SubnauticaSpeedrunningMod.Bootstrap.dll") -Destination (Join-Path $installModRoot "Bootstrap\SubnauticaSpeedrunningMod.Bootstrap.dll") -Force
Copy-Item -LiteralPath (Join-Path $bootstrapOutputRoot "Mono.Cecil.dll") -Destination (Join-Path $installModRoot "Bootstrap\Mono.Cecil.dll") -Force
Copy-Item -LiteralPath (Join-Path $runtimeOutputRoot "SubnauticaSpeedrunningMod.Runtime.dll") -Destination (Join-Path $installModRoot "Runtime\SubnauticaSpeedrunningMod.Runtime.dll") -Force
Copy-DirectoryContents -Source $updaterPublishRoot -Destination (Join-Path $installModRoot "Updater")
Copy-DirectoryContents -Source $bridgePublishRoot -Destination (Join-Path $installModRoot "Bridge")

Reset-Directory -Path (Join-Path $installModRoot "LanguageFiles")
Copy-DirectoryContents -Source $languageSourceRoot -Destination (Join-Path $installModRoot "LanguageFiles")

Reset-Directory -Path (Join-Path $installModRoot "SaveFiles")
Copy-DirectoryContents -Source $practiceSourceRoot -Destination (Join-Path $installModRoot "SaveFiles")

if (Test-Path -LiteralPath (Join-Path $installModRoot "Seeds\RankedSurvival")) {
    Remove-Item -LiteralPath (Join-Path $installModRoot "Seeds\RankedSurvival") -Recurse -Force
}
Get-ChildItem -LiteralPath (Join-Path $installModRoot "Seeds") -File -Filter "*SurvivalGlitchedAny%.csv" -ErrorAction SilentlyContinue |
    Remove-Item -Force
Copy-Item -LiteralPath $ruleset7SeedPoolPath -Destination (Join-Path $installModRoot "Seeds\1000SurvivalGlitchedAny%.csv") -Force

Remove-FilesByPattern -Root (Join-Path $installModRoot "Bridge") -Patterns @("*.pdb")

Write-Host "Creating release staging folder..."

Reset-Directory -Path $releaseRoot
Copy-Item -LiteralPath (Join-Path $GameInstallRoot ".doorstop_version") -Destination (Join-Path $releaseRoot ".doorstop_version") -Force
Copy-Item -LiteralPath (Join-Path $GameInstallRoot "doorstop_config.ini") -Destination (Join-Path $releaseRoot "doorstop_config.ini") -Force
Copy-Item -LiteralPath (Join-Path $GameInstallRoot "INSTALL.txt") -Destination (Join-Path $releaseRoot "INSTALL.txt") -Force
Copy-Item -LiteralPath (Join-Path $GameInstallRoot "Launch Mod.cmd") -Destination (Join-Path $releaseRoot "Launch Mod.cmd") -Force
Copy-Item -LiteralPath (Join-Path $GameInstallRoot "winhttp.dll") -Destination (Join-Path $releaseRoot "winhttp.dll") -Force

$releaseModRoot = Join-Path $releaseRoot "SubnauticaSpeedrunningMod"
Reset-Directory -Path $releaseModRoot

Copy-DirectoryContents -Source $installModRoot -Destination $releaseModRoot

$translationDictionaryPath = Join-Path $releaseModRoot "LanguageFiles\Translation Dictionary.txt"
if (Test-Path -LiteralPath $translationDictionaryPath) {
    Remove-Item -LiteralPath $translationDictionaryPath -Force
}

Reset-Directory -Path (Join-Path $releaseModRoot "Logs")
Ensure-Directory -Path (Join-Path $releaseModRoot "Logs\Bootstrap")
Ensure-Directory -Path (Join-Path $releaseModRoot "Logs\CrashReports")
Ensure-Directory -Path (Join-Path $releaseModRoot "Logs\Launcher")
Ensure-Directory -Path (Join-Path $releaseModRoot "Logs\Runtime")

Reset-Directory -Path (Join-Path $releaseModRoot "Client")
Reset-Directory -Path (Join-Path $releaseModRoot "Config")
if (Test-Path -LiteralPath (Join-Path $releaseModRoot "Seeds\State")) {
    Remove-Item -LiteralPath (Join-Path $releaseModRoot "Seeds\State") -Recurse -Force
}

Remove-FilesByPattern -Root $releaseModRoot -Patterns @("*.pdb", "*.cs")

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# Keep package files at the ZIP root for compatibility with every updater since Beta-0.8.0.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $releaseRoot,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
Test-ReleasePackage -PackagePath $zipPath -ExpectedVersion $VersionLabel -ExpectedSeedPoolPath $ruleset7SeedPoolPath
$zipSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Write-LatestManifest -ManifestPath $latestJsonPath -Version $VersionLabel -ZipFileName ([System.IO.Path]::GetFileName($zipPath)) -Sha256 $zipSha256

Write-Host "Release ready:"
Write-Host "  Folder: $releaseRoot"
Write-Host "  Zip:    $zipPath"
Write-Host "  latest: $latestJsonPath"
