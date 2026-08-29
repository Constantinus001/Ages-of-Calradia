$ErrorActionPreference = 'Stop'
$module = Split-Path -Parent $PSScriptRoot
$behavior = Get-Content -LiteralPath (Join-Path $module 'ReligionCampaignBehavior.cs') -Raw
$catalog = Get-Content -LiteralPath (Join-Path $module 'ReligionCatalog.cs') -Raw
$population = Get-Content -LiteralPath (Join-Path $module 'PopulationPersistence.cs') -Raw

$required = @(
    'AOCHEROFAITH1', 'AOCREALMFAITH1', 'AOCHOLYSITES1',
    'UniversalProtection', 'TraditionalTolerance', 'OfficialSupremacy', 'Suppression',
    'ReligiousTension', 'LastMonthlyConverts', 'FaithInstitutionStrengths',
    'aoc_religion_management', 'SponsorFestival', 'ConvertPlayer', 'CycleHolyAccess',
    'UndertakePilgrimage', 'ApplyMonthlyIncident', 'SectarianViolence', 'InterfaithFestival',
    'UpgradeInstitution', 'AppointClergy', 'EndowClergy', 'TaxClergy', 'CycleClergyGovernance', 'AOCCLERGYOFFICES1',
    'ProcessHeroReligionMonth', 'ProcessAnnualFaithRelations', 'ApplyGovernorFaithEffects', 'ReligiousLegitimacy', 'ConversionCount', 'BirthFaithId'
)
$combined = $behavior + (Get-Content -LiteralPath (Join-Path $module 'ReligionPersistence.cs') -Raw)
foreach ($token in $required) {
    if ($combined -notmatch [regex]::Escape($token) -and $population -notmatch [regex]::Escape($token)) {
        throw "Religion verification failed: missing $token"
    }
}

foreach ($faith in @('asharim','valeronism','mazirism','isharan_way','kok_orun_way','caerwydd','veyrhold','calradic_old_faith')) {
    if ($catalog -notmatch [regex]::Escape($faith)) { throw "Religion verification failed: missing faith $faith" }
}
if ($catalog -notmatch 'town_ES1' -or $catalog -notmatch 'danustica_three_testaments') {
    throw 'Religion verification failed: Danustica is not configured as the shared Aserac holy city.'
}
if ($population -notmatch 'AOCPOP5' -or $population -notmatch 'AOCPOP4' -or $population -notmatch 'AOCPOP3' -or $population -notmatch 'AOCPOP2' -or $population -notmatch 'AOCPOP1') {
    throw 'Religion verification failed: population save migration headers are incomplete.'
}
Write-Host 'Religion system verification passed: faith, clergy offices, institution tiers, tension, conversion, policies, holy sites, pilgrimages, and incidents are present.'
