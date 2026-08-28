$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$integration = Get-Content -LiteralPath (Join-Path $moduleRoot 'StrategicMapModeIntegration.cs') -Raw
$population = Get-Content -LiteralPath (Join-Path $moduleRoot 'PopulationCampaignBehavior.cs') -Raw
$report = Get-Content -LiteralPath (Join-Path $moduleRoot 'CensusReportBuilder.cs') -Raw
$textureProvider = Get-Content -LiteralPath (Join-Path $moduleRoot 'CensusPageTextureProvider.cs') -Raw
$prefab = Get-Content -LiteralPath (Join-Path $moduleRoot 'GUI\Prefabs\AocStrategicMapModes.xml') -Raw
$censusScene = Join-Path $moduleRoot 'GUI\CustomUI\WorldEventsSkin\page_cabinet_census_v1.png'
[xml]$null = $prefab

foreach ($required in @('Text="CENSUS"', 'Command.Click="ExecuteCensus"', '@CensusRealmPopulation',
    '@CensusCalradiaPopulation', '@CensusRealmCultures', '@CensusCalradiaCultures',
    '@CensusRealmReligions', '@CensusCalradiaReligions')) {
    if ($prefab -notmatch [regex]::Escape($required)) { throw "Census sidecar binding missing: $required" }
}
if ([regex]::Matches($prefab, 'Text="CENSUS"').Count -ne 1 -or
    $prefab -match 'Text="FINANCES"|Text="DIPLOMACY"|Text="MARRIAGES"') {
    throw 'The sidecar must add only one Census tab and must never redraw the three protected native tabs.'
}
if ($prefab -notmatch 'SuggestedWidth="101" SuggestedHeight="37"' -or
    $prefab -notmatch 'MarginLeft="148" MarginTop="216"' -or
    $prefab -notmatch 'MarginTop="254"') {
    throw 'The compact Census control/page is not aligned to unused space beside the protected original UI.'
}
if ($prefab -notmatch 'AocCensusPageTextureProvider' -or
    -not (Test-Path -LiteralPath $censusScene) -or
    $textureProvider -notmatch 'page_cabinet_census_v1\.png') {
    throw 'The sidecar Census page does not own its baked scene.'
}
if ([regex]::Matches($prefab, 'SuggestedWidth="486" SuggestedHeight="343"').Count -ne 2 -or
    [regex]::Matches($prefab, 'MarginTop="98"').Count -ne 2) {
    throw 'The two Census text ledgers are not aligned below the artwork medallions.'
}
if ($population -notmatch 'AOCCENSUS1' -or $population -notmatch 'FaithPopulations') { throw 'Live census payload is incomplete.' }
if ($report -notmatch 'Clan\.PlayerClan' -or $report -notmatch 'FormatCultures' -or $report -notmatch 'FormatReligions') { throw 'Kingdom and Calradia census aggregation is incomplete.' }
if ($integration -notmatch 'RefreshForMonthlyUpdate' -or $integration -notmatch 'RefreshCensus') { throw 'The census is not connected to monthly refresh.' }
foreach ($required in @('HideNativeRealmPageForCensus',
    'SetLedgerBoolean("IsKingdomFinancesPage", false)',
    'SetLedgerBoolean("IsDiplomacyRelationsPage", false)',
    'SetLedgerBoolean("IsMarriagesPage", true)',
    'SetLedgerBoolean("IsMarriagesPage", false)')) {
    if ($integration -notmatch [regex]::Escape($required)) { throw "Census native-selection suppression missing: $required" }
}

& (Join-Path $repositoryRoot 'Tests\Verify-ProtectedPoliticalBaseline.ps1')
Write-Host 'Single sidecar Census tab verification passed; protected UI and core hashes remain approved.'
