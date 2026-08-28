$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $moduleRoot)
$source = Get-Content -LiteralPath (Join-Path $moduleRoot 'StrategicMapModeIntegration.cs') -Raw
$populationSource = Get-Content -LiteralPath (Join-Path $moduleRoot 'PopulationCampaignBehavior.cs') -Raw
$prefabPath = Join-Path $moduleRoot 'GUI\Prefabs\AocStrategicMapModes.xml'
$prefabText = Get-Content -LiteralPath $prefabPath -Raw
[xml]$null = $prefabText

foreach ($binding in @(
    @{ Text = 'POL'; Command = 'ExecutePolitical' },
    @{ Text = 'REL'; Command = 'ExecuteReligion' },
    @{ Text = 'POP'; Command = 'ExecutePopulation' },
    @{ Text = 'CUL'; Command = 'ExecuteCulture' }
)) {
    if ($prefabText -notmatch ('Text="' + $binding.Text + '"')) { throw "Missing compact $($binding.Text) button." }
    if ($prefabText -notmatch ('Command.Click="' + $binding.Command + '"')) { throw "Missing $($binding.Command) binding." }
}

if ($prefabText -match 'Text="(?:POLITICAL|POPULATION)"') { throw 'Map-mode controls expanded into tab labels instead of compact buttons.' }
if ($prefabText -notmatch 'SuggestedWidth="286"' -or $prefabText -notmatch 'MarginLeft="87"' -or $prefabText -notmatch 'MarginTop="276"') { throw 'Compact controls moved outside the approved marked strip.' }
if ($source -notmatch 'layer\.LoadMovie\("AocStrategicMapModes"') { throw 'Overlay is not attached to the existing World Events Gauntlet layer.' }
if ($source -notmatch 'BeforeAtlasUpdate\(object\[\] __args\)' -or $source -notmatch '__args\[0\] = colours') { throw 'Strategic atlas colours are not replacing Harmony''s live argument array.' }
if ($source -notmatch 'NeutralProvinceColour' -or $source -notmatch 'colours\[entry\.Key\] = NeutralProvinceColour') { throw 'Demographic modes do not begin from a neutral province canvas.' }
if ($source -notmatch 'AfterStrategicMapLayersBuilt' -or $source -notmatch 'StrategicKingdomLabels') { throw 'Political kingdom labels are not suppressed on demographic maps.' }
if ($source -notmatch 'BuildDemographicMarkerSequence' -or $source -notmatch '__args\[1\]') { throw 'Demographic atlas markers still carry political owner colours.' }
if ($source -notmatch 'AfterProvinceColoursResolved' -or $source -notmatch 'AfterContestedColoursResolved') { throw 'Final atlas colour resolution can still overwrite demographic colours.' }
if ($source -notmatch '\[MAPTRACE\]' -or $source -notmatch 'AfterAtlasUpdate' -or $source -notmatch 'ColourArraySummary') { throw 'Strategic map diagnostic tracing is incomplete.' }
if ($source -notmatch 'AccessTools\.Method\(ledgerType, "SelectTab"\)' -or $source -notmatch 'SynchronizeOverlayVisibility') { throw 'Map-mode button visibility is not synchronized with the main Military Affairs tab.' }
if ($source -notmatch 'CurrentMode == StrategicMapMode\.Political' -or $source -notmatch 'ResolveReligionColour' -or $source -notmatch 'ResolvePopulationColour' -or $source -notmatch 'ResolveCultureColour') { throw 'One or more live map modes are missing.' }
foreach ($cultureColour in @('0xFFC9A66B', '0xFF557A32', '0xFF4B9B91', '0xFF9D443A', '0xFF17283D', '0xFF294F78', '0xFF77558E')) {
    if ($source -notmatch [regex]::Escape($cultureColour)) { throw "Required culture-map colour $cultureColour is missing." }
}
foreach ($religionColour in @('0xFF9F3F36', '0xFF78518F', '0xFFB77A2E', '0xFFD3AB3F', '0xFF3E938B', '0xFF4F7738', '0xFF2D527D', '0xFF80603D')) {
    if ($source -notmatch [regex]::Escape($religionColour)) { throw "Required religion-map colour $religionColour is missing." }
}
if ($populationSource -notmatch 'StrategicMapModeIntegration\.RefreshForMonthlyUpdate\(\)') { throw 'Monthly demographic updates do not refresh the active map mode.' }

& (Join-Path $repositoryRoot 'Tests\Verify-ProtectedPoliticalBaseline.ps1')

Write-Host 'Compact strategic map-mode integration verification passed.'
