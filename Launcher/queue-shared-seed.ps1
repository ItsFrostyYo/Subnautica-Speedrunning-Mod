param(
    [string] $GameRoot = "C:\Program Files (x86)\Steam\steamapps\common\Subnautica2018",
    [string] $Mode = "Survival",
    [string] $SeedId = "",
    [Parameter(Mandatory = $true)]
    [string] $SeedValue,
    [string] $Description = "Queued shared seed"
)

$ErrorActionPreference = "Stop"

$rankedRoot = Join-Path $GameRoot "SubnauticaSpeedrunningRanked"
$seedsRoot = Join-Path $rankedRoot "Data\Seeds"
$pendingPath = Join-Path $seedsRoot "pending-shared-seed.xml"

if ([string]::IsNullOrWhiteSpace($SeedId)) {
    switch ($Mode.ToLowerInvariant()) {
        "creative" { $SeedId = "Creative-Match" }
        "hardcore" { $SeedId = "Hardcore-Match" }
        default { $SeedId = "Survival-Match" }
    }
}

New-Item -ItemType Directory -Path $seedsRoot -Force | Out-Null

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RankedPendingSharedSeed xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <SeedId>$SeedId</SeedId>
  <SeedValue>$SeedValue</SeedValue>
  <GameMode>$Mode</GameMode>
  <Description>$Description</Description>
</RankedPendingSharedSeed>
"@

Set-Content -LiteralPath $pendingPath -Value $xml -Encoding UTF8

Write-Host "Queued shared seed at: $pendingPath"
Write-Host "Mode: $Mode"
Write-Host "SeedId: $SeedId"
Write-Host "SeedValue: $SeedValue"
