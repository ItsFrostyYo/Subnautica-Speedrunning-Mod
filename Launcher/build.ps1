$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "SubnauticaSpeedrunningRanked.sln"

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

if (-not (Test-Path $solution)) {
    throw "Solution not found: $solution"
}

Push-Location $root
try {
    Invoke-NativeCommand -FilePath dotnet -Arguments @("build", $solution, "-c", "Release")
}
finally {
    Pop-Location
}
