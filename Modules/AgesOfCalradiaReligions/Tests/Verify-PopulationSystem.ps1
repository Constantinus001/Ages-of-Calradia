$ErrorActionPreference = 'Stop'

$moduleRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Resolve-Path (Join-Path $moduleRoot '..\..')
$strategicCsv = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Ages Of Calradia\ModuleData\strategic_settlements_native.csv'

dotnet msbuild (Join-Path $PSScriptRoot 'PopulationSystemVerifier.csproj') /t:Rebuild /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Population verifier build failed.' }

& (Join-Path $PSScriptRoot 'bin\PopulationSystemVerifier.exe')
if ($LASTEXITCODE -ne 0) { throw 'Population domain verification failed.' }

$regions = Import-Csv -LiteralPath $strategicCsv | Where-Object { $_.Type -eq 'Town' -or $_.Type -eq 'Castle' }
if ($regions.Count -ne 133) { throw "Expected 133 strategic population regions; found $($regions.Count)." }

$majorIds = @(
    'town_A1', 'town_A2', 'town_A4', 'town_A6', 'town_A8',
    'town_EN1', 'town_EN2', 'town_EN6', 'town_ES1', 'town_ES4',
    'town_ES5', 'town_EW1', 'town_EW2', 'town_EW3', 'town_EW4',
    'town_V1', 'town_V3', 'town_V5', 'town_V6', 'town_V7'
)
$availableIds = @($regions | ForEach-Object { $_.SettlementId })
$missing = @($majorIds | Where-Object { $_ -notin $availableIds })
if ($majorIds.Count -ne 20 -or $missing.Count -ne 0) { throw "Major-city contract failed. Missing: $($missing -join ', ')." }

$peaceSource = Get-Content -Raw (Join-Path $moduleRoot 'OpeningPeaceBehavior.cs')
if ($peaceSource -notmatch 'TreatyDays\s*=\s*20') { throw 'Opening peace must remain exactly 20 days.' }
if ($peaceSource -notmatch 'WarDeclared' -or $peaceSource -notmatch 'MakePeaceAction\.Apply') { throw 'Opening peace does not enforce war cancellation.' }

$populationSource = Get-Content -Raw (Join-Path $moduleRoot 'PopulationCampaignBehavior.cs')
if ($populationSource -match 'PeoplePerGameTroop') { throw 'A soldier-to-represented-people ratio must not return.' }
if ($populationSource -notmatch 'demographicCost\s*=\s*amount\s*;') { throw 'Each recruited soldier must consume exactly one person of manpower.' }
foreach ($required in @('aoc_population_debug_town', 'aoc_population_debug_castle', 'aoc_population_debug_village',
    'aoc_debug_open_population_management', 'aoc_debug_refresh_population_report',
    'native tax multiplier', 'Mobilization: available', 'Garrison game capacity', 'Faith cohorts:')) {
    if ($populationSource -notmatch [regex]::Escape($required)) { throw "Settlement debug report is missing: $required" }
}

Write-Host 'Population integration verification passed: 133 regions, 20 major cities, forced 20-day peace, and settlement debug reporting.'
