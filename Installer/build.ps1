param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $root "src\SubnauticaSpeedrunningModInstaller\SubnauticaSpeedrunningModInstaller.csproj"
$outputPath = Join-Path $root "dist"

if (Test-Path $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $outputPath

Write-Host "Installer published to: $outputPath"
