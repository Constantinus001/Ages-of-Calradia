param(
    [Parameter(Mandatory = $true)]
    [string]$Archive
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archivePath = (Resolve-Path -LiteralPath $Archive).Path
$zip = [IO.Compression.ZipFile]::OpenRead($archivePath)

function Get-Entry([string]$name) {
    $entry = $zip.GetEntry($name)
    if ($null -eq $entry) { throw "Missing release entry: $name" }
    return $entry
}

function Read-EntryText([string]$name) {
    $reader = [IO.StreamReader]::new((Get-Entry $name).Open())
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

function Get-EntrySha256([string]$name) {
    $stream = (Get-Entry $name).Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
}

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("AgesOfCalradia-v1514-verify-" + [Guid]::NewGuid())
try {
    $entries = @($zip.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\', '/') })
    $outsideCore = @($entries | Where-Object { -not $_.StartsWith('AgesOfCalradia/', [StringComparison]::Ordinal) })
    if ($outsideCore.Count -gt 0) {
        throw "The release contains another module or top-level payload: $($outsideCore -join ', ')"
    }

    $forbidden = @($entries | Where-Object {
        $_ -match '(?i)(/AssetSources/|/DesignArchive/|/RecoveredSubModules/|\.pdb$|\.log$|diagnostic|\.bak$|\.before-|\.backup-)' -or
        $_ -match '(?i)tab_exact_|tab_strip_shell_|world_events_four_tabs_shell_buttonless'
    })
    if ($forbidden.Count -gt 0) {
        throw "Development, diagnostics, or obsolete UI content entered the player package: $($forbidden -join ', ')"
    }

    [xml]$manifest = Read-EntryText 'AgesOfCalradia/SubModule.xml'
    if ($manifest.Module.Id.value -ne 'AgesOfCalradia' -or $manifest.Module.Version.value -ne 'v1.5.14') {
        throw 'The package manifest is not the core AgesOfCalradia v1.5.14 module.'
    }
    if (@($manifest.Module.SubModules.SubModule | Where-Object {
        $_.Name.value -eq 'Ages of Calradia v1.5.14 Combined Fixes'
    }).Count -ne 1) {
        throw 'The v1.5.14 compatibility sidecar is not registered exactly once.'
    }

    $mainHash = Get-EntrySha256 'AgesOfCalradia/bin/Win64_Shipping_Client/AgesOfCalradia.dll'
    if ($mainHash -ne '560F1B5181F8CC2EFE51564D8675FD3089E722606FA55B0B166D36ECD9868D8E') {
        throw "Protected main DLL changed: $mainHash"
    }
    $worldEventsHash = Get-EntrySha256 'AgesOfCalradia/GUI/Prefabs/WorldCalendar/WorldCalendar.xml'
    if ($worldEventsHash -ne 'E7013CF2B18B381119CC7479F0840BC423CD59565913BD22BBFC1E0C55A82E5E') {
        throw "Protected World Events prefab changed: $worldEventsHash"
    }

    New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
    $sidecarPath = Join-Path $temporaryDirectory 'AgesOfCalradia.Approved560CalendarFixes.dll'
    $sidecarStream = (Get-Entry 'AgesOfCalradia/bin/Win64_Shipping_Client/AgesOfCalradia.Approved560CalendarFixes.dll').Open()
    $fileStream = [IO.File]::Create($sidecarPath)
    try { $sidecarStream.CopyTo($fileStream) }
    finally { $fileStream.Dispose(); $sidecarStream.Dispose() }
    $sidecarVersion = [Reflection.AssemblyName]::GetAssemblyName($sidecarPath).Version.ToString()
    if ($sidecarVersion -ne '1.5.14.0') {
        throw "Unexpected compatibility sidecar version: $sidecarVersion"
    }

    [xml]$mapBar = Read-EntryText 'AgesOfCalradia/GUI/Prefabs/Map/MapBar.xml'
    $clock = $mapBar.SelectSingleNode('//*[@Id="CalendarClockText"]')
    if ($null -eq $clock -or $clock.Text -ne '@TimeOfDay' -or
        $clock.SuggestedWidth -ne '70' -or $clock.SuggestedHeight -ne '36') {
        throw 'The two-line AM/PM campaign clock layout is missing.'
    }

    foreach ($required in @(
        'AgesOfCalradia/bin/Win64_Shipping_Client/0Harmony.dll',
        'AgesOfCalradia/GUI/Ages Of CalradiaSpriteData.xml',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/RuntimeAssetManifest.txt',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_calendar_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_diplomacy_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_finance_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_marriage_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_story_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/design_foreground_summaries_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/foreground_diplomacy.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_archive_row_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_calendar_v2.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_companions_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_diplomacy_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_marriage_row_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_marriage_v2.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_story_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_strategic_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_treasury_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/page_cabinet_war_statistics_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/tab_sub_inactive_outline_332x56_v1.png',
        'AgesOfCalradia/GUI/CustomUI/WorldEventsSkin/tab_sub_selected_gold_332x56_v1.png',
        'AgesOfCalradia/GUI/SpriteParts/ui_world_events_v2/aoc_world_events_shell_v8.png',
        'AgesOfCalradia/GUI/SpriteParts/ui_world_events_v6/aoc_world_events_shell_calendar_selected_v6.png',
        'AgesOfCalradia/GUI/SpriteParts/ui_world_events_v6/aoc_world_events_shell_story_selected_v6.png',
        'AgesOfCalradia/GUI/SpriteParts/ui_world_events_v6/aoc_world_events_shell_realm_selected_v6.png',
        'AgesOfCalradia/GUI/SpriteParts/ui_world_events_v6/aoc_world_events_shell_strategic_selected_v6.png',
        'AgesOfCalradia/Assets/GauntletUI/ui_world_events_v2_1_tex.tpac',
        'AgesOfCalradia/Assets/GauntletUI/ui_world_events_v6_1_tex.tpac',
        'AgesOfCalradia/RuntimeDataCache/2A1830F3-4740-45CC-9938-C6FAB79CFEC6.rdc',
        'AgesOfCalradia/RuntimeDataCache/0622BBE7-B8CC-4CDC-A98D-44F6C9335248.rdc')) {
        [void](Get-Entry $required)
    }

    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    Write-Output "PASS: v1.5.14 core-only player package verified. Entries=$($entries.Count); SizeMB=$([Math]::Round((Get-Item $archivePath).Length / 1MB, 2)); Sidecar=$sidecarVersion; Main=$mainHash; SHA256=$archiveHash"
}
finally {
    $zip.Dispose()
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
