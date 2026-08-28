$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$xmlPath = Join-Path $root 'GUI\Prefabs\WorldCalendar\WorldCalendar.xml'
[xml]$document = Get-Content -LiteralPath $xmlPath -Raw
if ($null -ne $document.SelectSingleNode("//*[@Id='WorldEventsMainEagleTop']") -or
    $null -ne $document.SelectSingleNode("//*[@Id='WorldEventsMainEagleBottom']")) {
    throw 'Legacy foreground eagle overlays are still present.'
}

$realmShell = $document.SelectSingleNode("//*[@Id='WorldEventsSelectedRealmShell']")
if ($null -eq $realmShell -or
    $realmShell.GetAttribute('IsVisible') -ne '@IsDiplomacyVisible' -or
    $realmShell.GetAttribute('Sprite') -ne 'aoc_world_events_shell_realm_selected_v6') {
    throw 'Realm Affairs is not using the approved section-wide shell with its baked eagle.'
}

if ($null -ne $document.SelectSingleNode("//*[@Id='RealmAffairsBakedEagleForeground']")) {
    throw 'Realm Affairs still contains a separate eagle overlay instead of relying on the baked shell.'
}

$expected = @{
    'GUI\SpriteParts\ui_world_events_v2\aoc_world_events_shell_v8.png' = '6E0527EDC76174E3C082035B8D2DDC5D77A22CF67C490421F9CB86555F1483B6'
    'GUI\SpriteParts\ui_world_events_v6\aoc_world_events_shell_calendar_selected_v6.png' = '4AED02AEF0079FFF0234CD99912F3E12146BD7B9805EBE6D023ED3B3A0D409A8'
    'GUI\SpriteParts\ui_world_events_v6\aoc_world_events_shell_realm_selected_v6.png' = 'DACFEE8E3FBD3532D1A04CEBF651E80D5DE2E11DF3959512C3A516054CB2996D'
    'GUI\SpriteParts\ui_world_events_v6\aoc_world_events_shell_story_selected_v6.png' = '6EB0ECC82F52E10251EC550341D6BF4EBB7979BEC0A95EC396B9AF5C7EFF7A0C'
    'GUI\SpriteParts\ui_world_events_v6\aoc_world_events_shell_strategic_selected_v6.png' = 'E8323AC6369D8D52A0BC165879F6AF23505B39F06DAC64EBD641D09CED90FB6D'
}

foreach ($entry in $expected.GetEnumerator()) {
    $path = Join-Path $root $entry.Key
    if (-not (Test-Path -LiteralPath $path)) { throw "Baked eagle shell is missing: $($entry.Key)" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $entry.Value) { throw "Baked eagle shell hash mismatch: $($entry.Key) = $hash" }
}

$packedExpected = @{
    'AssetSources\GauntletUI\ui_world_events_v2_1.png' = 'EF7729CB10C765BA625825F3BF8FB756216455299353F9FB6673DD6F2DFAEE9F'
    'AssetSources\GauntletUI\ui_world_events_v6_1.png' = '3F13E91450E7A40D449D6F61998988126F24CBF5A0DEFEA6E7DF218DE4947411'
    'Assets\GauntletUI\ui_world_events_v2_1_tex.tpac' = '82B9E537280FDD941AA1506FD2F221441D4F7C2E64D5E440E674F65F34BF1803'
    'Assets\GauntletUI\ui_world_events_v6_1_tex.tpac' = '47B3C9EB71631174F7DE999167DDF1A135288DDB4C2E5041A67E6BD5A0FBE998'
    'RuntimeDataCache\2A1830F3-4740-45CC-9938-C6FAB79CFEC6.rdc' = '30DA5C246B94B5065227296E063C6B74A23AB1C96B81AD8DBE3EBCCFD21CAFE4'
    'RuntimeDataCache\0622BBE7-B8CC-4CDC-A98D-44F6C9335248.rdc' = '26E542966DA5F7BF73FD8C7512DE28C965E0854C4B1616E1B461AED7D6BAF153'
}

foreach ($entry in $packedExpected.GetEnumerator()) {
    $path = Join-Path $root $entry.Key
    if (-not (Test-Path -LiteralPath $path)) { throw "Baked eagle runtime package is missing: $($entry.Key)" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $entry.Value) { throw "Baked eagle runtime package hash mismatch: $($entry.Key) = $hash" }
}

Write-Output 'World Events baked top/bottom eagle verification passed for every shell state and runtime package.'
