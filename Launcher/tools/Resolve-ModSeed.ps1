param(
    [string]$GameRoot = "",
    [string]$SeedFile,
    [string]$SeedId,
    [string]$SeedValue,
    [string]$SearchPrefix,
    [int]$SearchStart = 1,
    [int]$SearchCount = 1000,
    [string]$SortBy = "ScoreMaxRolls",
    [int]$Top = 10,
    [switch]$Descending,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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

if (-not ("ModSeedMath" -as [type])) {
    $seedMathCode = @"
using System;

public static class ModSeedMath
{
    public static double NextFloat(string rootKey, string scope, double minValue, double maxValue)
    {
        if (minValue > maxValue)
        {
            double swap = minValue;
            minValue = maxValue;
            maxValue = swap;
        }

        if (Math.Abs(maxValue - minValue) <= 0.0001d)
        {
            return minValue;
        }

        double unit = RollUnit(rootKey, scope);
        return minValue + (maxValue - minValue) * unit;
    }

    public static double NextSteppedFloat(string rootKey, string scope, double minValue, double maxValue, double step)
    {
        if (minValue > maxValue)
        {
            double swap = minValue;
            minValue = maxValue;
            maxValue = swap;
        }

        if (step <= 0d)
        {
            step = 0.001d;
        }

        if (Math.Abs(maxValue - minValue) <= 0.0001d)
        {
            return RoundToStep(minValue, step);
        }

        int bucketCount = Math.Max(1, (int)Math.Round((maxValue - minValue) / step, MidpointRounding.AwayFromZero));
        int bucketIndex = Math.Min(bucketCount, (int)Math.Floor(RollUnit(rootKey, scope) * (bucketCount + 1)));
        return RoundToStep(minValue + bucketIndex * step, step);
    }

    public static int NextSteppedInt(string rootKey, string scope, int minValue, int maxValue, int step)
    {
        if (minValue > maxValue)
        {
            int swap = minValue;
            minValue = maxValue;
            maxValue = swap;
        }

        if (step <= 0)
        {
            step = 1;
        }

        if (maxValue == minValue)
        {
            return minValue;
        }

        int bucketCount = Math.Max(1, (int)Math.Round((maxValue - minValue) / (double)step, MidpointRounding.AwayFromZero));
        int bucketIndex = Math.Min(bucketCount, (int)Math.Floor(RollUnit(rootKey, scope) * (bucketCount + 1)));
        return minValue + bucketIndex * step;
    }

    public static int SelectWeightedIndex(string rootKey, string scope, double[] weights)
    {
        if (weights == null || weights.Length == 0)
        {
            return -1;
        }

        double totalWeight = 0d;
        for (int i = 0; i < weights.Length; i++)
        {
            totalWeight += Math.Max(0d, weights[i]);
        }

        if (totalWeight <= 0d)
        {
            return 0;
        }

        double roll = NextFloat(rootKey, scope, 0d, totalWeight);
        double cursor = 0d;
        for (int i = 0; i < weights.Length; i++)
        {
            cursor += Math.Max(0d, weights[i]);
            if (roll <= cursor)
            {
                return i;
            }
        }

        return weights.Length - 1;
    }

    private static double RollUnit(string rootKey, string scope)
    {
        ulong hash = ComputeDeterministicHash(rootKey + "|" + (scope ?? string.Empty));
        ulong masked = hash & 0xFFFFFFFFFFFFUL;
        return masked / (double)0x1000000000000UL;
    }

    private static double RoundToStep(double value, double step)
    {
        if (step <= 0d)
        {
            return value;
        }

        decimal decimalStep = (decimal)step;
        decimal buckets = Math.Round((decimal)value / decimalStep, MidpointRounding.AwayFromZero);
        return (double)(buckets * decimalStep);
    }

    private static ulong ComputeDeterministicHash(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string normalized = string.IsNullOrEmpty(value) ? "seed" : value;
            for (int i = 0; i < normalized.Length; i++)
            {
                hash ^= normalized[i];
                hash *= prime;
            }

            return hash;
        }
    }
}
"@

    Add-Type -TypeDefinition $seedMathCode -Language CSharp
}

function Get-ActiveSeedPath {
    param([string]$ResolvedGameRoot)
    return Join-Path $ResolvedGameRoot "SubnauticaSpeedrunningMod\Data\Seeds\active-seed.xml"
}

function ConvertTo-OrderedMap {
    return New-Object System.Collections.Specialized.OrderedDictionary
}

function Get-XmlChildValue {
    param(
        $Node,
        [string]$Name
    )

    if ($null -eq $Node) {
        return $null
    }

    $child = $Node.SelectSingleNode($Name)
    if ($null -eq $child) {
        return $null
    }

    return [string]$child.InnerText
}

function Get-DoubleOrDefault {
    param(
        [string]$Value,
        [double]$Default = 0
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return [double]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-IntOrDefault {
    param(
        [string]$Value,
        [int]$Default = 0
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return [int]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-BoolOrDefault {
    param(
        [string]$Value,
        [bool]$Default = $false
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return [bool]::Parse($Value)
}

function Resolve-SeedDefinition {
    param(
        [xml]$SeedXml,
        [string]$ResolvedSeedId,
        [string]$ResolvedSeedValue
    )

    $hashSeedId = $ResolvedSeedId
    $hashSeedValue = $ResolvedSeedValue
    if ($hashSeedId -ieq "Survival-Singleplayer" -and $hashSeedValue -ieq "survival-singleplayer-default") {
        $hashSeedId = "creative-range-test"
        $hashSeedValue = "creative-range-test-default"
    }

    $rootKey = "$hashSeedId|$hashSeedValue"
    $survival = $SeedXml.ModSeedDefinition.Survival

    $result = [ordered]@{}
    $result.SeedId = $ResolvedSeedId
    $result.SeedValue = $ResolvedSeedValue
    $result.Description = [string]$SeedXml.ModSeedDefinition.Description
    $result.SeedFormat = "Deterministic string pair hashed via FNV-1a 64-bit."
    $result.HashSeedId = $hashSeedId
    $result.HashSeedValue = $hashSeedValue

    $ranges = @($survival.Spawn.Ranges.Range)
    if ($ranges.Count -gt 0) {
        $weights = New-Object double[] $ranges.Count
        for ($i = 0; $i -lt $ranges.Count; $i++) {
            $weights[$i] = Get-DoubleOrDefault $ranges[$i].Weight 0
        }

        $selectedIndex = [ModSeedMath]::SelectWeightedIndex($rootKey, "survival-spawn-range", $weights)
        if ($selectedIndex -lt 0) {
            $selectedIndex = 0
        }

        $selectedRange = $ranges[$selectedIndex]
        $rangeName = [string]$selectedRange.Name
        $spawnX = [ModSeedMath]::NextFloat($rootKey, "survival-spawn-x|$rangeName", (Get-DoubleOrDefault $selectedRange.MinX), (Get-DoubleOrDefault $selectedRange.MaxX))
        $spawnZ = [ModSeedMath]::NextFloat($rootKey, "survival-spawn-z|$rangeName", (Get-DoubleOrDefault $selectedRange.MinZ), (Get-DoubleOrDefault $selectedRange.MaxZ))

        $result.SurvivalSpawn = [ordered]@{
            Range = $rangeName
            X = [math]::Round($spawnX, 3)
            Z = [math]::Round($spawnZ, 3)
        }
    }

    $groups = [ordered]@{
        Fragments = @($survival.Fragments.Entry)
        Resources = @($survival.Resources.Entry)
        Creatures = @($survival.Creatures.Entry)
        Always = @($survival.Always.Entry)
        Biomes = @($survival.Biomes.Entry)
    }

    $seededRollCount = 0
    $maxedRollCount = 0

    foreach ($groupName in $groups.Keys) {
        $resolvedGroup = [ordered]@{}
        foreach ($entry in $groups[$groupName]) {
            if ($null -eq $entry) {
                continue
            }

            $name = [string]$entry.Name
            $useSeedRange = Get-BoolOrDefault $entry.UseSeedRange $false
            if ($useSeedRange) {
                $minValue = Get-DoubleOrDefault $entry.MinChanceMultiplier
                $maxValue = Get-DoubleOrDefault $entry.MaxChanceMultiplier
                $stepValue = Get-DoubleOrDefault $entry.ResolutionStep 0.001
                $resolvedValue = [ModSeedMath]::NextSteppedFloat($rootKey, "survival|$groupName|$name", $minValue, $maxValue, $stepValue)
                $seededRollCount++
                if ([math]::Abs($resolvedValue - $maxValue) -le 0.0001) {
                    $maxedRollCount++
                }
            }
            else {
                $resolvedValue = Get-DoubleOrDefault $entry.ChanceMultiplier 1
            }

            $resolvedGroup[$name] = [math]::Round($resolvedValue, 3)
        }

        $result[$groupName] = $resolvedGroup
    }

    $manualCreatureSpawns = [ordered]@{}
    foreach ($entry in @($survival.ManualCreatureSpawns.Entry)) {
        if ($null -eq $entry) {
            continue
        }

        $techTypeName = [string]$entry.TechTypeName
        $useSeedRange = Get-BoolOrDefault $entry.UseSeedRange $false
        if ($useSeedRange) {
            $minAmount = Get-IntOrDefault $entry.MinAmount 0
            $maxAmount = Get-IntOrDefault $entry.MaxAmount 0
            $amountStep = Get-IntOrDefault $entry.AmountStep 1
            $resolvedAmount = [ModSeedMath]::NextSteppedInt($rootKey, "survival|ManualCreatureSpawns|$techTypeName|Amount", $minAmount, $maxAmount, $amountStep)
        }
        else {
            $resolvedAmount = Get-IntOrDefault $entry.Amount 0
        }

        $manualCreatureSpawns[$techTypeName] = $resolvedAmount
    }
    $result.ManualCreatureSpawns = $manualCreatureSpawns

    $alwaysBiomeMultipliers = [ordered]@{}
    foreach ($entry in @($survival.AlwaysBiomeMultipliers.Entry)) {
        if ($null -eq $entry) {
            continue
        }

        $alwaysBiomeMultipliers[[string]$entry.Name] = [math]::Round((Get-DoubleOrDefault $entry.ChanceMultiplier 1), 3)
    }
    $result.AlwaysBiomeMultipliers = $alwaysBiomeMultipliers

    $alwaysBiomeTechMultipliers = [ordered]@{}
    foreach ($entry in @($survival.AlwaysBiomeTechMultipliers.Entry)) {
        if ($null -eq $entry) {
            continue
        }

        $alwaysBiomeTechMultipliers["$($entry.BiomeName):$($entry.TechTypeName)"] = [math]::Round((Get-DoubleOrDefault $entry.ChanceMultiplier 1), 3)
    }
    $result.AlwaysBiomeTechMultipliers = $alwaysBiomeTechMultipliers

    $result.Score = [ordered]@{
        SeededRollCount = $seededRollCount
        MaxRollCount = $maxedRollCount
        MaxRollRatio = if ($seededRollCount -gt 0) { [math]::Round($maxedRollCount / $seededRollCount, 4) } else { 0 }
    }

    return $result
}

function Get-PathValue {
    param(
        $Object,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    $current = $Object
    foreach ($segment in $Path.Split('.')) {
        if ($null -eq $current) {
            return $null
        }

        if ($current -is [System.Collections.IDictionary]) {
            if (-not $current.Contains($segment)) {
                return $null
            }

            $current = $current[$segment]
            continue
        }

        $property = $current.PSObject.Properties[$segment]
        if ($null -eq $property) {
            return $null
        }

        $current = $property.Value
    }

    return $current
}

function Format-ResolvedSeedText {
    param($ResolvedSeed)

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine("SeedId=$($ResolvedSeed.SeedId)")
    [void]$builder.AppendLine("SeedValue=$($ResolvedSeed.SeedValue)")
    [void]$builder.AppendLine("SeedFormat=$($ResolvedSeed.SeedFormat)")
    [void]$builder.AppendLine("HashSeedId=$($ResolvedSeed.HashSeedId)")
    [void]$builder.AppendLine("HashSeedValue=$($ResolvedSeed.HashSeedValue)")
    [void]$builder.AppendLine("Description=$($ResolvedSeed.Description)")
    [void]$builder.AppendLine("SurvivalSpawnRange=$($ResolvedSeed.SurvivalSpawn.Range)")
    [void]$builder.AppendLine("SurvivalSpawnX=$($ResolvedSeed.SurvivalSpawn.X)")
    [void]$builder.AppendLine("SurvivalSpawnZ=$($ResolvedSeed.SurvivalSpawn.Z)")

    foreach ($groupName in @("Fragments", "Resources", "Creatures", "Always", "Biomes", "ManualCreatureSpawns", "AlwaysBiomeMultipliers", "AlwaysBiomeTechMultipliers")) {
        [void]$builder.AppendLine("[$groupName]")
        foreach ($key in $ResolvedSeed[$groupName].Keys) {
            [void]$builder.AppendLine("$key=$($ResolvedSeed[$groupName][$key])")
        }
    }

    [void]$builder.AppendLine("[Score]")
    foreach ($key in $ResolvedSeed.Score.Keys) {
        [void]$builder.AppendLine("$key=$($ResolvedSeed.Score[$key])")
    }

    return $builder.ToString().TrimEnd()
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Resolve-DefaultGameRoot
}

if ([string]::IsNullOrWhiteSpace($SeedFile)) {
    $SeedFile = Get-ActiveSeedPath -ResolvedGameRoot $GameRoot
}

if (-not (Test-Path -LiteralPath $SeedFile)) {
    throw "Seed file not found: $SeedFile"
}

[xml]$seedXml = Get-Content -LiteralPath $SeedFile
$fileSeedId = [string]$seedXml.ModSeedDefinition.SeedId
$fileSeedValue = [string]$seedXml.ModSeedDefinition.SeedValue

if ([string]::IsNullOrWhiteSpace($SeedId)) {
    $SeedId = $fileSeedId
}

if ([string]::IsNullOrWhiteSpace($SeedValue)) {
    if (-not [string]::IsNullOrWhiteSpace($fileSeedValue) -and $SeedId -eq $fileSeedId) {
        $SeedValue = $fileSeedValue
    }
    else {
        $SeedValue = "$SeedId-default"
    }
}

if (-not [string]::IsNullOrWhiteSpace($SearchPrefix)) {
    $resolvedSeeds = New-Object System.Collections.Generic.List[object]
    for ($offset = 0; $offset -lt $SearchCount; $offset++) {
        $candidateValue = "{0}-{1}" -f $SearchPrefix, ($SearchStart + $offset)
        $resolved = Resolve-SeedDefinition -SeedXml $seedXml -ResolvedSeedId $SeedId -ResolvedSeedValue $candidateValue
        $sortValue = Get-PathValue -Object $resolved -Path $SortBy
        $resolvedSeeds.Add([pscustomobject]@{
            SeedId = $resolved.SeedId
            SeedValue = $resolved.SeedValue
            SortBy = $SortBy
            SortValue = $sortValue
            ScoreMaxRolls = $resolved.Score.MaxRollCount
            SurvivalSpawnRange = $resolved.SurvivalSpawn.Range
            SurvivalSpawnX = $resolved.SurvivalSpawn.X
            SurvivalSpawnZ = $resolved.SurvivalSpawn.Z
            StalkerAmount = $resolved.ManualCreatureSpawns.Stalker
        })
    }

    if ($Descending) {
        $results = $resolvedSeeds | Sort-Object -Property SortValue, SeedValue -Descending | Select-Object -First $Top
    }
    else {
        $results = $resolvedSeeds | Sort-Object -Property SortValue, SeedValue | Select-Object -First $Top
    }

    if ($Json) {
        $results | ConvertTo-Json -Depth 6
    }
    else {
        $results | Format-Table -AutoSize
    }

    return
}

$resolvedSeed = Resolve-SeedDefinition -SeedXml $seedXml -ResolvedSeedId $SeedId -ResolvedSeedValue $SeedValue

if ($Json) {
    $resolvedSeed | ConvertTo-Json -Depth 10
}
else {
    Format-ResolvedSeedText -ResolvedSeed $resolvedSeed
}
