param(
    [string]$PrefabPath = (Join-Path $PSScriptRoot '..\GUI\Prefabs\WorldCalendar\WorldCalendar.xml'),
    [string]$ProviderPath = (Join-Path $PSScriptRoot '..\WorldEventsSkinTextureProviders.cs'),
    [string]$ScreenPath = (Join-Path $PSScriptRoot '..\WorldCalendarScreen.cs'),
    [string]$ViewModelPath = (Join-Path $PSScriptRoot '..\CalendarWorldLedgerVM.cs'),
    [string]$CombatStatisticsPath = (Join-Path $PSScriptRoot '..\CalendarHeroCombatStatisticsMissionBehavior.cs'),
    [string]$OriginCapturePath = (Join-Path $PSScriptRoot '..\CharacterOriginStoryCapture.cs'),
    [string]$MapScrollerPath = (Join-Path $PSScriptRoot '..\StrategicMapZoomScrollablePanel.cs'),
    [string]$ShellPath = (Join-Path $PSScriptRoot '..\GUI\CustomUI\WorldEventsSkin\world_events_four_tabs_shell_buttonless_v7_full_bottom_eagle.png'),
    [string]$SpriteDataPath = (Join-Path $PSScriptRoot '..\GUI\Ages Of CalradiaSpriteData.xml'),
    [string]$ImportedAtlasPath = (Join-Path $PSScriptRoot '..\AssetSources\GauntletUI\ui_world_events_v2_1.png'),
    [string]$TexturePackagePath = (Join-Path $PSScriptRoot '..\Assets\GauntletUI\ui_world_events_v2_1_tex.tpac'),
    [string]$RuntimeTextureCachePath = (Join-Path $PSScriptRoot '..\RuntimeDataCache\2A1830F3-4740-45CC-9938-C6FAB79CFEC6.rdc'),
    [string]$SelectedAtlasPath = (Join-Path $PSScriptRoot '..\AssetSources\GauntletUI\ui_world_events_v6_1.png'),
    [string]$SelectedTexturePackagePath = (Join-Path $PSScriptRoot '..\Assets\GauntletUI\ui_world_events_v6_1_tex.tpac'),
    [string]$SelectedRuntimeTextureCachePath = (Join-Path $PSScriptRoot '..\RuntimeDataCache\0622BBE7-B8CC-4CDC-A98D-44F6C9335248.rdc'),
    [string]$SelectedStateDirectory = (Join-Path $PSScriptRoot '..\GUI\SpriteParts\ui_world_events_v6')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PrefabPath)) {
    throw "World Events prefab was not found: $PrefabPath"
}

if (-not (Test-Path -LiteralPath $ProviderPath)) {
    throw "World Events texture provider was not found: $ProviderPath"
}

if (-not (Test-Path -LiteralPath $ScreenPath)) {
    throw "World Events screen host was not found: $ScreenPath"
}

if (-not (Test-Path -LiteralPath $ShellPath)) {
    throw "World Events shell artwork was not found: $ShellPath"
}

foreach ($importedAssetPath in @($SpriteDataPath, $ImportedAtlasPath, $TexturePackagePath, $RuntimeTextureCachePath, $SelectedAtlasPath, $SelectedTexturePackagePath, $SelectedRuntimeTextureCachePath)) {
    if (-not (Test-Path -LiteralPath $importedAssetPath -PathType Leaf)) {
        throw "World Events Resource Browser output was not found: $importedAssetPath"
    }
    if ((Get-Item -LiteralPath $importedAssetPath).Length -eq 0) {
        throw "World Events Resource Browser output is empty: $importedAssetPath"
    }
}

[xml]$spriteData = Get-Content -Raw -LiteralPath $SpriteDataPath
$worldEventsV2Categories = @($spriteData.SpriteData.SpriteCategories.SpriteCategory | Where-Object { $_.Name -eq 'ui_world_events_v2' })
if ($worldEventsV2Categories.Count -ne 1 -or
    [int]$worldEventsV2Categories[0].SpriteSheetCount -ne 1 -or
    [int]$worldEventsV2Categories[0].SpriteSheetSize.Width -ne 4096 -or
    [int]$worldEventsV2Categories[0].SpriteSheetSize.Height -ne 1024 -or
    $null -eq $worldEventsV2Categories[0].AlwaysLoad) {
    throw 'World Events Resource Browser category must be one AlwaysLoad 4096x1024 sheet.'
}
$worldEventsV2Parts = @($spriteData.SpriteData.SpriteParts.SpritePart | Where-Object { $_.CategoryName -eq 'ui_world_events_v2' })
$worldEventsV2PartNames = @($worldEventsV2Parts | ForEach-Object { [string]$_.Name })
$worldEventsV2Sprites = @($spriteData.SpriteData.Sprites.GenericSprite | Where-Object { $worldEventsV2PartNames -contains [string]$_.SpritePartName })
if ($worldEventsV2Parts.Count -ne 5 -or $worldEventsV2Sprites.Count -ne 5) {
    throw 'World Events Resource Browser metadata must contain five v2 sprite parts and five generic sprites.'
}
$worldEventsV6Categories = @($spriteData.SpriteData.SpriteCategories.SpriteCategory | Where-Object { $_.Name -eq 'ui_world_events_v6' })
$worldEventsV6Parts = @($spriteData.SpriteData.SpriteParts.SpritePart | Where-Object { $_.CategoryName -eq 'ui_world_events_v6' })
$worldEventsV6PartNames = @($worldEventsV6Parts | ForEach-Object { [string]$_.Name })
$worldEventsV6Sprites = @($spriteData.SpriteData.Sprites.GenericSprite | Where-Object { $worldEventsV6PartNames -contains [string]$_.SpritePartName })
if ($worldEventsV6Categories.Count -ne 1 -or
    $null -eq $worldEventsV6Categories[0].AlwaysLoad -or
    [int]$worldEventsV6Categories[0].SpriteSheetSize.Width -ne 4096 -or
    [int]$worldEventsV6Categories[0].SpriteSheetSize.Height -ne 2048 -or
    $worldEventsV6Parts.Count -ne 4 -or
    $worldEventsV6Sprites.Count -ne 4) {
    throw 'World Events selected-shell category must be one AlwaysLoad 4096x2048 sheet with four sprites.'
}

[xml]$prefab = Get-Content -Raw -LiteralPath $PrefabPath
$xmlText = Get-Content -Raw -LiteralPath $PrefabPath
[xml]$xml = $xmlText
$providerText = Get-Content -Raw -LiteralPath $ProviderPath
$screenText = Get-Content -Raw -LiteralPath $ScreenPath
$viewModelText = Get-Content -Raw -LiteralPath $ViewModelPath
$combatStatisticsText = Get-Content -Raw -LiteralPath $CombatStatisticsPath
$originCaptureText = Get-Content -Raw -LiteralPath $OriginCapturePath
$mapScrollerText = Get-Content -Raw -LiteralPath $MapScrollerPath

function Assert-Contains([string]$Text, [string]$Expected, [string]$Description) {
    if (-not $Text.Contains($Expected)) {
        throw "World Events layout contract failed ($Description): missing '$Expected'."
    }
}

function Assert-NotContains([string]$Text, [string]$Unexpected, [string]$Description) {
    if ($Text.Contains($Unexpected)) {
        throw "World Events layout contract failed ($Description): unexpected '$Unexpected'."
    }
}

# The authored v7 cabinet was proportionally reduced from its 1400-by-1000
# working canvas to the original window footprint.  Keep the layout contract
# here, rather than allowing the old full-size assertions below to silently
# validate a mismatched border, art, and hit-target coordinate system.
$frame = $prefab.SelectSingleNode('//*[@Id="WorldEventsFrame"]')
if ($null -eq $frame -or $frame.SuggestedWidth -ne '1220' -or $frame.SuggestedHeight -ne '871' -or
    $frame.PositionXOffset -ne '23' -or $frame.PositionYOffset -ne '90') {
    throw 'World Events scaled-layout contract failed: the cabinet must be 1220 by 871, natively centered on the physical-viewport sundial and 90 down to the map bar.'
}

$mainTab = $prefab.SelectSingleNode('//ListPanel[@DataSource="{Tabs}"]/ItemTemplate/ButtonWidget')
if ($null -eq $mainTab -or $mainTab.GetAttribute('SuggestedWidth') -ne '290' -or $mainTab.GetAttribute('SuggestedHeight') -ne '49' -or
    $mainTab.GetAttribute('Command.Click') -ne 'ExecuteSelect' -or $mainTab.GetAttribute('DoNotPassEventsToChildren') -ne 'true') {
    throw 'World Events scaled-layout contract failed: main-tab hit targets no longer match the 1157-pixel authored rail.'
}

$pageCommands = @(
    'ExecuteShowCalendarPage', 'ExecuteShowSavedSummaries',
    'ExecuteShowCharacterStory', 'ExecuteShowCompanionsPage',
    'ExecuteShowKingdomFinances', 'ExecuteShowDiplomacyRelations',
    'ExecuteShowMarriagesPage', 'ExecuteShowStrategicMapPage',
    'ExecuteShowStrategicWarStatistics'
)
foreach ($command in $pageCommands) {
    $button = $prefab.SelectSingleNode("//*[@Command.Click='$command']")
    if ($null -eq $button -or $button.GetAttribute('SuggestedWidth') -ne '218' -or
        $button.GetAttribute('HeightSizePolicy') -ne 'StretchToParent' -or $button.GetAttribute('DoNotPassEventsToChildren') -ne 'true') {
        throw "World Events scaled-layout contract failed: $command has no aligned 218-pixel page-tab hit target."
    }
}

foreach ($navigation in @(
    @{ Id = 'CalendarSectionNavigation'; Width = '453'; Height = '70'; Top = '200' },
    @{ Id = 'StorySectionNavigation'; Width = '453'; Height = '37'; Top = '216' },
    @{ Id = 'RealmSectionNavigation'; Width = '688'; Height = '37'; Top = '216' },
    @{ Id = 'StrategicSectionNavigation'; Width = '453'; Height = '37'; Top = '216' }
)) {
    $node = $prefab.SelectSingleNode("//*[@Id='$($navigation.Id)']")
    if ($null -eq $node -or $node.SuggestedWidth -ne $navigation.Width -or
        $node.SuggestedHeight -ne $navigation.Height -or $node.MarginTop -ne $navigation.Top -or
        $node.HorizontalAlignment -ne 'Center') {
        throw "World Events scaled-layout contract failed: $($navigation.Id) is not on the shared page-tab baseline."
    }
}

$calendarArt = $prefab.SelectSingleNode('//*[@Id="CalendarPageCabinetArt"]')
$calendarContent = $prefab.SelectSingleNode('//*[@Id="CalendarEditableContentPanel"]')
$calendarNotes = $prefab.SelectSingleNode('//*[@Id="CalendarEditableNotesPanel"]')
$calendarGrid = $prefab.SelectSingleNode('//*[@Id="CalendarEditableDayGrid"]')
if ($null -eq $calendarArt -or $null -eq $calendarContent -or $null -eq $calendarNotes -or
    $calendarArt.SuggestedWidth -ne '1115' -or $calendarArt.SuggestedHeight -ne '528' -or
    $calendarArt.MarginTop -ne '263' -or $calendarContent.SuggestedWidth -ne '758' -or
    $calendarContent.SuggestedHeight -ne '528' -or $calendarContent.MarginLeft -ne '52' -or
    $calendarContent.MarginTop -ne '263' -or $calendarNotes.SuggestedWidth -ne '331' -or
    $calendarNotes.SuggestedHeight -ne '528' -or $calendarNotes.MarginRight -ne '52' -or
    $calendarNotes.MarginTop -ne '263' -or $null -eq $calendarGrid -or
    $calendarGrid.DataSource -ne '{Days}' -or $calendarGrid.DefaultCellWidth -ne '102' -or
    $calendarGrid.DefaultCellHeight -ne '63' -or $calendarGrid.ColumnCount -ne '7' -or
    $calendarGrid.MarginLeft -ne '30' -or $calendarGrid.MarginTop -ne '93') {
    throw 'World Events scaled-layout contract failed: blank calendar art and its editable controls are no longer co-located.'
}

# The 1180x590 cabinet assets map to 1028x514 and begin at the authored
# 257 baseline. Moving the whole page changes nothing inside the cabinet and
# breaks the generated-design registration.
$authoredPanelBaselines = @{
    SavedSummariesPanel = '257'
    MarriagesPanel = '239'
    CharacterStoryPanel = '257'
    CompanionsPanel = '257'
    DiplomacyPanel = '239'
    WarStatisticsPanel = '257'
}
foreach ($panelId in $authoredPanelBaselines.Keys) {
    $panel = $prefab.SelectSingleNode("//*[@Id='$panelId']")
    if ($null -eq $panel -or $panel.MarginTop -ne $authoredPanelBaselines[$panelId]) {
        throw "World Events scaled-layout contract failed: $panelId moved away from its authored cabinet baseline."
    }
}
$strategicArt = $prefab.SelectSingleNode('//*[@Id="StrategicPageCabinetArt"]')
$strategicSidePanel = $prefab.SelectSingleNode('//*[@Id="StrategicSidePanel"]')
if ($null -eq $strategicArt -or $strategicArt.MarginTop -ne '257' -or
    $null -eq $strategicSidePanel -or $strategicSidePanel.MarginTop -ne '257') {
    throw 'World Events scaled-layout contract failed: the strategic cabinet moved away from its authored baseline.'
}

$calendarTitle = $calendarContent.SelectSingleNode("./Children/TextWidget[@Id='CalendarLiveHeading']")
$calendarPager = $calendarContent.SelectSingleNode("./Children/Widget[@Id='CalendarMonthPager']")
$calendarMonthTitle = $calendarPager.SelectSingleNode("./Children/TextWidget[@Id='CalendarLiveMonthTitle']")
$calendarPrevious = $calendarPager.SelectSingleNode("./Children/ButtonWidget[@Id='CalendarPreviousMonth']")
$calendarNext = $calendarPager.SelectSingleNode("./Children/ButtonWidget[@Id='CalendarNextMonth']")
if ($null -eq $calendarTitle -or $calendarTitle.WidthSizePolicy -ne 'Fixed' -or
    $calendarTitle.Text -ne 'CAMPAIGN CALENDAR' -or $calendarTitle.SuggestedWidth -ne '500' -or
    $calendarTitle.MarginLeft -ne '134' -or $calendarTitle.MarginTop -ne '8' -or
    $null -eq $calendarPager -or $calendarPager.SuggestedHeight -ne '504' -or
    $calendarPager.MarginTop -ne '24' -or $null -eq $calendarMonthTitle -or
    $calendarMonthTitle.Text -ne '@MonthTitle' -or $calendarMonthTitle.SuggestedWidth -ne '430' -or
    $calendarMonthTitle.MarginLeft -ne '164' -or $calendarMonthTitle.MarginTop -ne '27' -or
    $null -eq $calendarPrevious -or $calendarPrevious.SuggestedWidth -ne '68' -or
    $calendarPrevious.SuggestedHeight -ne '38' -or $calendarPrevious.MarginLeft -ne '34' -or
    $calendarPrevious.MarginTop -ne '3' -or $calendarPrevious.GetAttribute('Command.Click') -ne 'ExecutePreviousCalendarMonth' -or
    $null -eq $calendarNext -or $calendarNext.SuggestedWidth -ne '68' -or
    $calendarNext.SuggestedHeight -ne '38' -or $calendarNext.MarginLeft -ne '680' -or
    $calendarNext.MarginTop -ne '3' -or $calendarNext.GetAttribute('Command.Click') -ne 'ExecuteNextCalendarMonth') {
    throw 'World Events scaled-layout contract failed: Calendar heading, live month, pager controls, and day grid no longer match the approved plate.'
}

$calendarNotesTitle = $calendarNotes.SelectSingleNode("./Children/TextWidget[@Id='CalendarSelectedDateTitle']")
$calendarNotesText = $calendarNotes.SelectSingleNode("./Children/TextWidget[@Id='CalendarSelectedDateNotes']")
if ($null -eq $calendarNotesTitle -or $calendarNotesTitle.Text -ne '@NotesTitle' -or
    $calendarNotesTitle.SuggestedWidth -ne '60' -or $calendarNotesTitle.SuggestedHeight -ne '64' -or
    $calendarNotesTitle.PositionXOffset -ne '-14' -or $calendarNotesTitle.MarginTop -ne '53' -or
    $calendarNotesTitle.GetAttribute('Brush.FontSize') -ne '13' -or
    $calendarNotesTitle.ClipContents -ne 'true' -or
    $calendarNotesTitle.IsVisible -ne '@HasSelectedCalendarDay' -or
    $null -eq $calendarNotesText -or $calendarNotesText.Text -ne '@NotesText' -or
    $calendarNotesText.SuggestedWidth -ne '245' -or $calendarNotesText.SuggestedHeight -ne '261' -or
    $calendarNotesText.MarginTop -ne '157' -or $calendarNotesText.GetAttribute('Brush.FontSize') -ne '15' -or
    $calendarNotesText.Color -ne '#E7DDC8FF' -or $calendarNotesText.ClipContents -ne 'true') {
    throw 'World Events scaled-layout contract failed: Calendar Day Record live text no longer fits the approved medallion and notes aperture.'
}
$calendarNotesClearance = [int]$calendarNotesText.MarginTop -
    ([int]$calendarNotesTitle.MarginTop + [int]$calendarNotesTitle.SuggestedHeight)
if ($calendarNotesClearance -lt 40) {
    throw "Calendar notes overlap regression: selected-date title/body clearance is only $calendarNotesClearance units."
}
# The reviewed cabinet medallion is centered at source X=1074 on its
# 1280-pixel texture. Map that through the 1115-wide live plate and compare it
# with the live title center in the right-anchored 331-wide notes panel.
$calendarMedallionCenterX = ((1220 - 1115) / 2) + (1074 * 1115 / 1280)
$calendarDateTitleCenterX = (1220 - 52 - 331) + (331 / 2) + [int]$calendarNotesTitle.PositionXOffset
if ([math]::Abs($calendarMedallionCenterX - $calendarDateTitleCenterX) -gt 1) {
    throw "Calendar date alignment regression: live title center $calendarDateTitleCenterX does not match medallion center $calendarMedallionCenterX."
}

$archivePanel = $prefab.SelectSingleNode('//*[@Id="SavedSummariesPanel"]')
$archiveScroller = $prefab.SelectSingleNode('//*[@Id="SavedSummariesScroller"]')
$archiveBackdrop = $prefab.SelectSingleNode('//*[@Id="SavedSummariesBackdrop"]')
$archiveRow = $prefab.SelectSingleNode('//*[@Id="SavedSummaryRow"]')
$archiveRowArt = $archiveRow.SelectSingleNode('./Children/TextureWidget[@Id="SavedSummaryRowArt"]')
if ($null -eq $archivePanel -or $archivePanel.SuggestedWidth -ne '1028' -or
    $archivePanel.SuggestedHeight -ne '514' -or $archivePanel.MarginTop -ne '257' -or
    $null -eq $archiveScroller -or $archiveScroller.LocalName -ne 'ScrollablePanel' -or
    $archiveScroller.SuggestedWidth -ne '871' -or $archiveScroller.SuggestedHeight -ne '322' -or
    $archiveScroller.MarginLeft -ne '74' -or $archiveScroller.MarginTop -ne '160' -or
    $archiveScroller.HasAttribute('RowStride') -or $null -eq $archiveBackdrop -or
    $archiveBackdrop.IsVisible -ne 'false' -or $null -eq $archiveRow -or
    $archiveRow.SuggestedHeight -ne '149' -or $null -eq $archiveRowArt -or
    $archiveRowArt.IsVisible -ne 'true' -or
    $archiveRowArt.TextureProviderName -ne 'WorldEventsArchiveRowTextureProvider') {
    throw 'World Events scaled-layout contract failed: Summaries variable-height scroll-owned cards no longer match the approved archive aperture.'
}

$strategicPanel = $prefab.SelectSingleNode('//*[@Id="StrategicMapPanel"]')
$strategicCanvas = $prefab.SelectSingleNode('//*[@Id="StrategicMapCanvas"]')
if ($null -eq $strategicPanel -or $strategicPanel.SuggestedWidth -ne '741' -or $strategicPanel.SuggestedHeight -ne '427' -or
    $strategicPanel.MarginLeft -ne '87' -or $strategicPanel.MarginTop -ne '305' -or
    $null -eq $strategicCanvas -or $strategicCanvas.SuggestedWidth -ne '@MapCanvasWidth' -or $strategicCanvas.SuggestedHeight -ne '@MapCanvasHeight' -or
    -not $viewModelText.Contains('private const float StrategicMapViewportWidth = 741f;') -or
    -not $viewModelText.Contains('private const float StrategicMapViewportHeight = 427f;')) {
    throw 'World Events scaled-layout contract failed: strategic viewport and canvas constants are mismatched.'
}

Write-Host 'World Events scaled 1220x871 layout verification passed (23 right, 90 down).'
return

Assert-Contains $xmlText 'Id="WorldEventsFrame"' 'main frame'
Assert-Contains $xmlText 'SuggestedWidth="1400" SuggestedHeight="1000"' 'main frame dimensions'
Assert-NotContains $xmlText 'Id="WorldEventsCustomShell"' 'obsolete compiled shell underlay removed'
Assert-NotContains $xmlText 'Sprite="world_events_shell_current_tabs_alpha"' 'compiled shell cannot tint the transparent outer canvas'
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsShellTextureProvider"' 'oversized runtime shell texture provider is not used by the prefab'
Assert-Contains $xmlText 'Id="CalendarContentPanel"' 'calendar panel'
Assert-Contains $xmlText 'SuggestedWidth="870" SuggestedHeight="606"' 'calendar workspace dimensions'
Assert-Contains $xmlText 'Id="CalendarNotesPanel"' 'notes panel'
Assert-Contains $xmlText 'SuggestedWidth="380" SuggestedHeight="606"' 'notes panel dimensions'
Assert-Contains $xmlText 'DefaultCellWidth="117" DefaultCellHeight="62"' 'calendar cells track the authored 117-by-62 engraved lanes'
Assert-NotContains $xmlText 'Id="CalendarSummaryPanel"' 'duplicate month/year summary boxes removed from Calendar'
Assert-NotContains $xmlText 'Id="MonthlySummaryPanel"' 'duplicate monthly summary box removed from Calendar'
Assert-NotContains $xmlText 'Id="YearlySummaryPanel"' 'duplicate yearly summary box removed from Calendar'
Assert-Contains $xmlText 'Brush="TownManagement.GovernorPopup.GoldFrame"' 'native gold frame treatment'
Assert-Contains $xmlText 'DataSource="{Tabs}"' 'tab data binding'
Assert-Contains $xmlText 'Command.Click="ExecuteSelect"' 'tab selection command'
Assert-NotContains $xmlText 'AcceptDrag=' 'finalized tabs are not draggable'
Assert-NotContains $xmlText 'Command.DragBegin=' 'finalized tabs have no drag commands'
Assert-NotContains $viewModelText 'WorldEventsTabLayoutStore' 'temporary tab-order persistence removed'
$preferredTabDeclarations = @(
    'AddTab("Calendar", "Realm Chronicle");',
    'AddTab("Character", "My Story");',
    'AddTab("Diplomacy", "Realm Affairs");',
    'AddTab("Strategic", "Military Affairs");'
)
$previousTabDeclaration = -1
foreach ($declaration in $preferredTabDeclarations) {
    $tabDeclaration = $viewModelText.IndexOf($declaration, [StringComparison]::Ordinal)
    if ($tabDeclaration -le $previousTabDeclaration) {
        throw "World Events layout contract failed (preferred default tab order): '$declaration' is missing or out of order."
    }
    $previousTabDeclaration = $tabDeclaration
}
Assert-NotContains $xmlText 'Id="IntegratedCloseButton"' 'obsolete close button removed'
Assert-NotContains $xmlText 'Id="IntegratedRefreshButton"' 'obsolete refresh button removed'
Assert-Contains $screenText '_layer.Input.IsHotKeyReleased("Exit") || _layer.Input.IsKeyReleased(InputKey.Escape)' 'Escape closes World Events'
Assert-Contains $screenText 'private const float AutomaticRefreshInterval = 1f;' 'automatic one-second refresh cadence'
Assert-Contains $xmlText 'Id="WorldEventsFullBorderShell"' 'continuous full-perimeter cabinet shell'
Assert-Contains $xmlText 'Sprite="aoc_world_events_shell_v8"' 'Resource Browser full-border shell sprite'
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsFullBorderShellTextureProvider"' 'runtime full-border shell provider removed from prefab'
Assert-Contains $xmlText 'Id="WorldEventsLiveTitle"' 'crisp live World Events title'
Assert-Contains $xmlText 'Text="World Events" Color="#D8BD83FF"' 'centered World Events title text'
Assert-Contains $xmlText 'Id="WorldEventsLiveSubtitle"' 'crisp live World Events subtitle'
Assert-Contains $xmlText 'Text="CHRONICLE  ·  REALM  ·  STRATEGIC OVERVIEW"' 'approved live subtitle wording'
Assert-Contains $xmlText 'Brush.Font="Galahad"' 'Galahad UI typography'
Assert-NotContains $xmlText 'FourTabHeaderSubtitleBackdrop' 'obsolete subtitle black mask'
Assert-NotContains $xmlText 'FourTabRailBackdrop' 'obsolete tab-rail black mask'
Assert-Contains $providerText 'AssetName { get { return "tab_selected_five_v1"; } }' 'five-tab selected asset provider'
Assert-Contains $providerText 'pixels[pixelOffset] = red;' 'BGRA-to-RGBA red channel correction'
Assert-Contains $providerText 'pixels[pixelOffset + 2] = blue;' 'BGRA-to-RGBA blue channel correction'
Assert-Contains $xmlText 'SuggestedWidth="@TabWidth" SuggestedHeight="56" Command.Click="ExecuteSelect"' 'reference-sized finalized tab hit strip'
Assert-Contains $xmlText 'MarginTop="151" StackLayout.LayoutMethod="HorizontalLeftToRight"' 'approved-shell-aligned four-tab hit strip'
Assert-NotContains $xmlText 'Id="WorldEventsFiveTabBackdrop"' 'obsolete baked tab strip concealment removed'
Assert-NotContains $xmlText 'Id="ExactFourTabStrip"' 'duplicate live four-tab rail removed'
Assert-NotContains $xmlText 'TextureProviderName="@SelectedTabTextureProvider"' 'obsolete per-slot selected frames removed'
Assert-Contains $xmlText 'Id="WorldEventsSelectedCalendarShell" IsVisible="@IsCalendarSectionVisible"' 'Realm Chronicle selected shell remains highlighted across Calendar and Summaries'
Assert-Contains $xmlText 'Id="WorldEventsSelectedStoryShell" IsVisible="@IsStorySectionVisible"' 'story selected shell uses stable visibility binding'
Assert-Contains $xmlText 'Id="WorldEventsSelectedRealmShell" IsVisible="@IsDiplomacyVisible"' 'realm selected shell uses stable visibility binding'
Assert-Contains $xmlText 'Id="WorldEventsSelectedStrategicShell" IsVisible="@IsStrategicSectionVisible"' 'strategic selected shell uses stable visibility binding'
Assert-NotContains $xmlText 'TextureProviderName="@MainTabStripTextureProvider"' 'unreliable dynamic texture-provider binding removed'
Assert-Contains $xmlText 'Sprite="aoc_world_events_shell_calendar_selected_v6"' 'calendar uses the imported full selected shell'
Assert-Contains $xmlText 'Sprite="aoc_world_events_shell_story_selected_v6"' 'story uses the imported full selected shell'
Assert-Contains $xmlText 'Sprite="aoc_world_events_shell_realm_selected_v6"' 'realm uses the imported full selected shell'
Assert-Contains $xmlText 'Sprite="aoc_world_events_shell_strategic_selected_v6"' 'strategic uses the imported full selected shell'
Assert-NotContains $xmlText 'Id="WorldEventsSelectedCalendarTabStrip"' 'obsolete independent selected rail removed'
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsExactCalendarTabStripTextureProvider"' 'runtime calendar selected rail provider removed from prefab'
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsTabActiveTextureProvider"' 'oversized generic selected artwork removed'
Assert-NotContains $xmlText 'TextureProviderName="@SkinIconProvider"' 'generic live circular icons removed from the main tabs'
Assert-NotContains $xmlText 'Brush="Popup.SelectionElement.Tuple" Command.Click="ExecuteSelect"' 'native tab chrome removed from compiled shell'
Assert-NotContains $xmlText 'Text="@Label" Color="#DFC68FFF"' 'runtime font overlay removed from the main tabs'
Assert-Contains $xmlText 'IsVisible="false" DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="850" SuggestedHeight="28"' 'calendar instruction line hidden'
Assert-Contains $xmlText 'Id="SavedSummariesScroller" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="920"' 'authored-background-aligned saved-summary archive cards'
Assert-Contains $xmlText 'Id="SavedSummariesBackdrop" DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="920" SuggestedHeight="370"' 'archive viewport masks obsolete fixed record frames'
Assert-Contains $xmlText 'Id="SavedSummaryToggle" IsEnabled="true"' 'every archived record exposes a live toggle button'
Assert-Contains $xmlText 'Id="SavedSummaryToggle" IsEnabled="true" DoNotPassEventsToChildren="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Command.Click="ExecuteToggle"' 'archive toggle owns the topmost full-card hit target'
Assert-Contains $xmlText 'Text="@ActionText"' 'archive record action reports its live open/close state'
Assert-Contains $viewModelText 'public string ActionText' 'archive record exposes an open/close action label'
Assert-Contains $viewModelText 'yearImportantCount == 0' 'empty yearly records use a compact expanded body'
Assert-Contains $viewModelText 'eventCount == 0' 'empty monthly records use a compact expanded body'
Assert-Contains $xmlText 'Id="SavedSummaryRowArt" DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" TextureProviderName="WorldEventsArchiveRowTextureProvider"' 'archive artwork moves with its interactive record'
Assert-Contains $providerText 'class WorldEventsArchiveRowTextureProvider' 'archive records have a dedicated authored texture provider'
$archiveRowAsset = Join-Path $PSScriptRoot '..\GUI\CustomUI\WorldEventsSkin\page_cabinet_archive_row_v1.png'
if (-not (Test-Path -LiteralPath $archiveRowAsset)) {
    throw 'World Events layout contract failed (Archive scrolling): authored moving-record texture is missing.'
}
Assert-Contains $xmlText 'Text="CHRONICLE ARCHIVE"' 'saved-summary archive heading'
Assert-Contains $xmlText 'Text="MONTHLY AND YEARLY RECORDS OF THE REALM"' 'saved-summary archive subtitle'
Assert-Contains $xmlText 'Id="StrategicSummaryText" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" VerticalAlignment="Top" Brush="Popup.Button.Text" Brush.FontSize="15"' 'readable strategic summary text'
Assert-NotContains $xmlText 'Id="SelectedTabArtwork"' 'individually stretched tab artwork removed'
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsFiveTabSelectedTextureProvider"' 'blank selected overlay that concealed baked text removed'
Assert-NotContains $xmlText 'AlphaFactor="0.65"' 'translucent internal-page selection removed'
Assert-Contains $xmlText 'Color="#C89432FF" AlphaFactor="0.28"' 'subtle marriage sort selection that preserves the authored button engraving'
Assert-NotContains $xmlText 'Color="#C89432FF" AlphaFactor="1.0"' 'opaque marriage sort fill no longer conceals the authored cabinet'
Assert-NotContains $xmlText 'Color="#A97825FF" AlphaFactor="1.0"' 'old muted internal-page selection removed'
Assert-NotContains $xmlText 'Id="TabIcon"' 'main-tab icons are baked into measured artwork'
Assert-NotContains $xmlText 'Id="TabLabel"' 'main-tab labels are baked into measured artwork'
Assert-NotContains $xmlText 'Id="ForeignOfficeInactiveTab"' 'Foreign Office inactive replacement is baked into the atlas'
Assert-NotContains $xmlText 'Id="ForeignOfficeIcon"' 'Foreign Office icon is baked into its sprite'
Assert-NotContains $xmlText 'Id="ForeignOfficeLabel"' 'Foreign Office label is baked into its sprite'
Assert-NotContains $xmlText 'MarginLeft="45" MarginRight="9" MarginTop="13" MarginBottom="13" Sprite="BlankWhiteSquare"' 'obsolete Foreign Office text mask removed'
Assert-Contains $xmlText 'WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="@TabWidth" SuggestedHeight="56" Command.Click="ExecuteSelect"' 'transparent main-tab hit targets retain measured bounds'
Assert-NotContains $xmlText 'ButtonType="Radio"' 'native radio selection art removed from custom tabs'
Assert-NotContains $xmlText 'MarginLeft="12" MarginRight="12" MarginTop="10" MarginBottom="10" Sprite="BlankWhiteSquare_9" Color="#6B4A20FF"' 'old inset selected-tab overlay removed'
Assert-NotContains $xmlText 'SuggestedHeight="3" VerticalAlignment="Bottom" MarginLeft="10" MarginRight="10" Sprite="BlankWhiteSquare_9" Color="#D0A75EFF"' 'out-of-tab selected underline removed'
Assert-NotContains $xmlText '#1D3D4AFF' 'old blue-black frame accents removed'
Assert-NotContains $xmlText '#47798AFF' 'old cyan frame highlights removed'
Assert-Contains $viewModelText '[DataSourceProperty] public int TabWidth' 'tab width binding source'
Assert-NotContains $viewModelText 'MainTabStripTextureProvider' 'unreliable dynamic main-strip provider state removed'
Assert-Contains $combatStatisticsText '!affectedAgent.IsHuman' 'combat statistics exclude mounts and non-human agents'
Assert-Contains $originCaptureText 'ReferenceEquals(_activeManager, manager)' 'abandoned character creation is isolated'
Assert-Contains $originCaptureText 'private static void Clear()' 'transient origin choices have one cleanup path'
Assert-Contains $viewModelText 'private const float StrategicMapMinimumZoom = 1f;' 'map cannot shrink short of its fitted border'
Assert-Contains $mapScrollerText 'base.OnLateUpdate(dt);' 'Bannerlord scrollbar range update precedes zoom correction'
Assert-Contains $mapScrollerText 'InnerPanel.ScaledPositionXOffset = -target;' 'same-frame horizontal center correction'
Assert-Contains $mapScrollerText 'InnerPanel.ScaledPositionYOffset = -target;' 'same-frame vertical center correction'
Assert-Contains $mapScrollerText 'if (!IsVisible)' 'hidden strategic map cancels panning'
Assert-Contains $mapScrollerText 'SetActiveCursor(UIContext.MouseCursors.Default);' 'strategic move cursor is reset outside panning'
Assert-Contains $viewModelText 'return 332;' 'standardized 332-pixel tab width'
$mainTabButton = $prefab.SelectSingleNode('//ListPanel[@DataSource="{Tabs}"]/ItemTemplate/ButtonWidget')
if ($null -eq $mainTabButton -or $mainTabButton.SuggestedHeight -ne '56' -or $mainTabButton.ClipContents -ne 'true') {
    throw 'World Events layout contract failed (main tab slot): main tabs must use the clipped 332x56 state slot.'
}
$mainShells = @(
    @{ Id = 'WorldEventsSelectedCalendarShell'; Visibility = '@IsCalendarSectionVisible'; Sprite = 'aoc_world_events_shell_calendar_selected_v6' },
    @{ Id = 'WorldEventsSelectedStoryShell'; Visibility = '@IsStorySectionVisible'; Sprite = 'aoc_world_events_shell_story_selected_v6' },
    @{ Id = 'WorldEventsSelectedRealmShell'; Visibility = '@IsDiplomacyVisible'; Sprite = 'aoc_world_events_shell_realm_selected_v6' },
    @{ Id = 'WorldEventsSelectedStrategicShell'; Visibility = '@IsStrategicSectionVisible'; Sprite = 'aoc_world_events_shell_strategic_selected_v6' }
)
foreach ($expectedShell in $mainShells) {
    $mainShell = $prefab.SelectSingleNode("//Widget[@Id='$($expectedShell.Id)']")
    if ($null -eq $mainShell -or
        $mainShell.WidthSizePolicy -ne 'StretchToParent' -or
        $mainShell.HeightSizePolicy -ne 'StretchToParent' -or
        $mainShell.GetAttribute('IsVisible') -ne $expectedShell.Visibility -or
        $mainShell.GetAttribute('Sprite') -ne $expectedShell.Sprite) {
        throw "World Events layout contract failed (main selected shell): $($expectedShell.Id) must use its imported sprite on the shared 1400x1000 shell canvas."
    }
}
if ($mainTabButton.SelectNodes('./Children/*').Count -ne 0) {
    throw 'World Events layout contract failed (main tab hit targets): button slots must not draw independent artwork over the connected strip.'
}
$selectedShellAssets = @(
    @{ Name = 'aoc_world_events_shell_calendar_selected_v6.png'; Hash = '4AED02AEF0079FFF0234CD99912F3E12146BD7B9805EBE6D023ED3B3A0D409A8' },
    @{ Name = 'aoc_world_events_shell_story_selected_v6.png'; Hash = '6EB0ECC82F52E10251EC550341D6BF4EBB7979BEC0A95EC396B9AF5C7EFF7A0C' },
    @{ Name = 'aoc_world_events_shell_realm_selected_v6.png'; Hash = 'DACFEE8E3FBD3532D1A04CEBF651E80D5DE2E11DF3959512C3A516054CB2996D' },
    @{ Name = 'aoc_world_events_shell_strategic_selected_v6.png'; Hash = 'E8323AC6369D8D52A0BC165879F6AF23505B39F06DAC64EBD641D09CED90FB6D' }
)
Add-Type -AssemblyName System.Drawing
foreach ($asset in $selectedShellAssets) {
    $assetPath = Join-Path $SelectedStateDirectory $asset.Name
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "World Events layout contract failed (selected shell asset missing): $assetPath"
    }
    $bitmap = [Drawing.Bitmap]::FromFile($assetPath)
    try {
        if ($bitmap.Width -ne 1400 -or $bitmap.Height -ne 1000) {
            throw "World Events layout contract failed (selected shell dimensions): $($asset.Name) is $($bitmap.Width)x$($bitmap.Height), expected 1400x1000."
        }
    }
    finally {
        $bitmap.Dispose()
    }
    $actualHash = (Get-FileHash -LiteralPath $assetPath -Algorithm SHA256).Hash
    if ($actualHash -ne $asset.Hash) {
        throw "World Events layout contract failed (reviewed selected shell hash): $($asset.Name) changed after alignment review."
    }
}
Assert-Contains $xmlText 'Id="StorySectionNavigation" IsVisible="@IsStorySectionVisible"' 'My Story internal page navigation'
Assert-Contains $xmlText 'Command.Click="ExecuteShowCharacterStory"' 'My Story character page command'
Assert-Contains $xmlText 'Command.Click="ExecuteShowCompanionsPage"' 'My Story companions page command'
Assert-Contains $xmlText 'Id="CalendarSectionNavigation" IsVisible="@IsCalendarSectionVisible"' 'Calendar internal page navigation'
Assert-Contains $xmlText 'Id="CalendarSectionNavigation" IsVisible="@IsCalendarSectionVisible" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="520" SuggestedHeight="80" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="230"' 'Calendar page navigation has full-height live hit targets centered on the approved visual baseline'
Assert-Contains $xmlText 'Id="StorySectionNavigation" IsVisible="@IsStorySectionVisible" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="520" SuggestedHeight="42" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="248"' 'My Story page navigation aligned with Calendar'
Assert-Contains $xmlText 'Id="RealmSectionNavigation" IsVisible="@IsDiplomacyVisible" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="790" SuggestedHeight="42" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="248"' 'Realm Affairs page navigation aligned with Calendar'
Assert-Contains $xmlText 'Id="StrategicSectionNavigation" IsVisible="@IsStrategicSectionVisible" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="520" SuggestedHeight="42" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="248"' 'Strategic page navigation aligned with Calendar'
Assert-Contains $xmlText 'Command.Click="ExecuteShowCalendarPage"' 'Calendar page command'
Assert-Contains $xmlText 'Command.Click="ExecuteShowSavedSummaries"' 'Summaries page under Calendar'
Assert-Contains $xmlText 'SuggestedHeight="42" VerticalAlignment="Center"><Children><TextureWidget' 'Calendar/Summaries artwork centered inside the enlarged live buttons'
Assert-NotContains $xmlText 'Id="CalendarSectionNavigationHitTargets"' 'no detached invisible Calendar/Summaries hit-target overlay'
Assert-Contains $xmlText 'Id="SavedSummariesPanel" IsVisible="@IsSavedSummariesPage" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="1180" SuggestedHeight="590" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="295"' 'Saved Summaries content positioned 20 pixels below its previous baseline'
Assert-Contains $xmlText 'Id="MarriageNameSearchInput"' 'marriage candidate name search field'
Assert-Contains $xmlText 'Text="@MarriageNameSearchText"' 'marriage search two-way text binding'
Assert-Contains $viewModelText '[DataSourceProperty] public string MarriageNameSearchText' 'marriage name search binding source'
Assert-Contains $viewModelText 'IndexOf(_marriageNameSearchText.Trim(), StringComparison.OrdinalIgnoreCase)' 'case-insensitive marriage candidate name filter'
Assert-Contains $xmlText 'Id="StrategicSectionNavigation" IsVisible="@IsStrategicSectionVisible"' 'Strategic internal page navigation'
Assert-Contains $xmlText 'Command.Click="ExecuteShowStrategicMapPage"' 'Strategic Map page command'
Assert-Contains $xmlText 'Command.Click="ExecuteShowStrategicWarStatistics"' 'War Statistics page command'
Assert-Contains $xmlText 'Id="RealmSectionNavigation" IsVisible="@IsDiplomacyVisible"' 'Realm Affairs internal page navigation'
Assert-Contains $xmlText 'Text="ROYAL TREASURY"' 'Kingdom Finances section heading'
Assert-Contains $xmlText 'Text="DIPLOMATIC CORRESPONDENCE"' 'Diplomacy section heading'
Assert-Contains $xmlText 'Text="MARRIAGE COURT"' 'Marriages section heading'
Assert-Contains $xmlText 'Text="CAMPAIGN CALENDAR"' 'Calendar content heading'
Assert-Contains $xmlText 'Text="DAY RECORD"' 'Calendar day-record heading'
Assert-Contains $xmlText 'IsVisible="@IsToday"' 'Calendar current-day highlight'
Assert-Contains $viewModelText '[DataSourceProperty] public bool IsToday' 'Calendar current-day binding source'
Assert-Contains $xmlText 'Text="PERSONAL CHRONICLE"' 'My Story content heading'
Assert-Contains $xmlText 'Text="COMPANY ROSTER"' 'Companions content heading'
Assert-Contains $xmlText 'Text="YOUR COMPANY AWAITS"' 'Companions empty-state heading'
Assert-Contains $xmlText 'Text="WAR ROOM LEDGER"' 'Strategic Map side-panel heading'
Assert-Contains $xmlText 'MarginTop="@StrategicMapLegendContentTop"' 'Strategic Map legend offset below heading'
Assert-Contains $viewModelText 'public int StrategicMapLegendContentTop' 'Strategic Map legend layout binding source'
foreach ($pageArt in 'Treasury','Diplomacy','Marriage','Calendar','Archive','Strategic','WarStatistics','Story','Companions') {
    $providerName = 'WorldEvents' + $pageArt + 'PageTextureProvider'
    Assert-Contains $xmlText ('TextureProviderName="' + $providerName + '"') "authored page artwork provider $providerName"
    Assert-Contains $providerText ('class ' + $providerName + ' : WorldEventsSkinTextureProvider') "authored page artwork provider class $providerName"
}
Assert-Contains $xmlText 'Text="KINGDOM FINANCES"' 'Kingdom Finances first internal page'
Assert-Contains $xmlText 'Text="DIPLOMACY"' 'Diplomacy second internal page'
$pageNavigationIds = @('CalendarSectionNavigation','StorySectionNavigation','RealmSectionNavigation','StrategicSectionNavigation')
$pageButtons = @()
foreach ($navigationId in $pageNavigationIds) {
    $pageButtons += @($prefab.SelectNodes("//ListPanel[@Id='$navigationId']/Children/ButtonWidget"))
}
if ($pageButtons.Count -ne 9) {
    throw "World Events layout contract failed (equal page tabs): expected 9 internal page buttons, found $($pageButtons.Count)."
}
foreach ($pageButton in $pageButtons) {
    if ($pageButton.SuggestedWidth -ne '250' -or $pageButton.HeightSizePolicy -ne 'StretchToParent' -or $pageButton.ClipContents -ne 'true') {
        throw 'World Events layout contract failed (equal page tabs): every internal page button must use the shared 250x42 clipped slot.'
    }
    if ($pageButton.HasAttribute('Sprite')) {
        throw 'World Events layout contract failed (equal page tabs): native popup artwork must not compete with the shared page-tab texture.'
    }
    $pageTextures = @($pageButton.SelectNodes('.//TextureWidget[@TextureProviderName="WorldEventsSubTabSelectedTextureProvider"]'))
    if ($pageTextures.Count -ne 1 -or -not $pageTextures[0].HasAttribute('IsVisible')) {
        throw 'World Events layout contract failed (page selection): gold page-tab artwork must render only for the selected page.'
    }
    $inactivePageTextures = @($pageButton.SelectNodes('.//TextureWidget[@TextureProviderName="WorldEventsSubTabInactiveTextureProvider"]'))
    if ($inactivePageTextures.Count -ne 1 -or $inactivePageTextures[0].HasAttribute('IsVisible')) {
        throw 'World Events layout contract failed (page borders): every page tab must retain exactly one always-visible inactive outline.'
    }
}
Assert-NotContains $xmlText 'TextureProviderName="WorldEventsSubTabSelectedTextureProvider" AlphaFactor="0.32"' 'inactive page tabs do not retain a gold fill'
Assert-Contains $xmlText 'Id="RealmSectionNavigation" IsVisible="@IsDiplomacyVisible" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="790"' 'three equal page tabs with uniform 20-pixel gaps'
Assert-Contains $viewModelText 'IsDiplomacyRelationsPage = false;' 'Kingdom defaults away from Diplomacy'
Assert-Contains $viewModelText 'IsKingdomFinancesPage = true;' 'Kingdom defaults to Finances'
Assert-NotContains $xmlText 'Id="IntegratedCloseButton"' 'buttonless shell has no Close hit target'
Assert-NotContains $xmlText 'Id="IntegratedRefreshButton"' 'automatic refresh replaces the Refresh hit target'
Assert-Contains $xmlText 'Id="CharacterPortraitFrame"' 'bronze-framed character portrait'
Assert-Contains $xmlText 'Id="CharacterPortraitViewport"' 'aspect-safe portrait viewport'
Assert-Contains $xmlText 'SuggestedWidth="250" SuggestedHeight="250"' 'square character portrait render target'

foreach ($obsoleteTan in '#17100CE0', '#B68A38FF', '#80652CFF') {
    if ($xmlText.Contains($obsoleteTan)) {
        throw "World Events layout contract failed (unified bronze styling): obsolete tan colour remains: $obsoleteTan."
    }
}

$opaqueTabHitTarget = 'SuggestedWidth="185" SuggestedHeight="60" Sprite="BlankWhiteSquare"'
if ($xmlText.Contains($opaqueTabHitTarget)) {
    throw 'World Events layout contract failed (transparent tab hit targets): the tab row would paint an opaque white strip over the authored shell.'
}

Assert-NotContains $xmlText 'Id="WorldEventsSubtitle"' 'approved subtitle is baked once into the shell'
Assert-Contains $xmlText 'Id="CalendarNotesPanel" IsVisible="false" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="380" SuggestedHeight="606" HorizontalAlignment="Right" VerticalAlignment="Top" MarginRight="60" MarginTop="302">' 'legacy calendar notes disabled beneath foreground design'
Assert-Contains $xmlText 'Color="#5C4325FF"' 'bronze cabinet dividers'
Assert-Contains $xmlText 'Id="CalendarNotesStrategicRail" IsVisible="false"' 'authored calendar art owns the notes divider without a duplicate rail'
Assert-Contains $xmlText 'SuggestedWidth="8" SuggestedHeight="606" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="941" MarginTop="302"' 'calendar rail placement'
Assert-Contains $xmlText 'Color="#8A6738FF"' 'calendar rail inner highlight'
Assert-Contains $xmlText 'Id="StrategicMapPanel" IsVisible="@IsStrategicMap" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="850" SuggestedHeight="490"' 'strategic map inset inside its authored cabinet aperture'
Assert-Contains $xmlText 'Id="StrategicMapPanel" IsVisible="@IsStrategicMap" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="850" SuggestedHeight="490" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="100" MarginTop="350"' 'strategic map aligned to the authored aperture origin'
Assert-Contains $xmlText 'Id="StrategicSidePanel" IsVisible="@IsStrategicMap" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="380" SuggestedHeight="590"' 'strategic legend fitted below its lowered internal navigation'
Assert-Contains $xmlText 'Id="StrategicMapLiveBorder"' 'framed strategic map border'
Assert-Contains $xmlText 'Id="StrategicLegendLiveBorder"' 'framed strategic legend border'

$warPanel = $xml.SelectSingleNode('//*[@Id="WarStatisticsPanel"]')
$warScrollerCabinet = $warPanel.SelectSingleNode('.//Widget[@SuggestedWidth="1060" and @SuggestedHeight="325" and @MarginTop="218"]')
$warFootnote = $warPanel.SelectSingleNode('.//TextWidget[@Text="@WarStatisticsFootnote"]')
if ($null -eq $warScrollerCabinet -or $null -eq $warFootnote) {
    throw 'World Events layout contract failed (War Statistics geometry): expected cabinet or footnote was not found.'
}
$warCabinetBottom = [int]$warScrollerCabinet.MarginTop + [int]$warScrollerCabinet.SuggestedHeight
$warFootnoteTop = [int]$warPanel.SuggestedHeight - [int]$warFootnote.MarginBottom - [int]$warFootnote.SuggestedHeight
if ($warCabinetBottom -gt $warFootnoteTop) {
    throw 'World Events layout contract failed (War Statistics geometry): the war list paints underneath its footnote.'
}
Assert-Contains $xmlText 'Id="WarStatisticsPageCabinetArt" DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" TextureProviderName="WorldEventsWarStatisticsPageTextureProvider"' 'War Statistics uses authored cabinet artwork in the foreground'
Assert-Contains $xmlText 'Id="WarStatisticsScroller" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" MarginLeft="8" MarginRight="8" MarginTop="0" MarginBottom="8"' 'War Statistics rows begin at the first authored lane'
$warRowTemplate = $warPanel.SelectSingleNode('.//ListPanel[@Id="WarStatisticsRows"]/ItemTemplate/Widget')
if ($null -eq $warRowTemplate -or $warRowTemplate.SuggestedHeight -ne '75') {
    throw 'World Events layout contract failed (War Statistics geometry): each live war row must fit one 75-pixel engraved lane.'
}

$calendarPager = $xml.SelectSingleNode('//*[@Id="CalendarMonthPager"]')
$calendarWeekdays = $calendarPager.SelectSingleNode('./Children/ListPanel[@SuggestedWidth="819" and @MarginTop="72"]')
$calendarGrid = $calendarPager.SelectSingleNode('./Children/GridWidget')
if ($calendarPager.MarginLeft -ne '0' -or $calendarPager.MarginRight -ne '0' -or $calendarWeekdays.MarginLeft -ne '60' -or $calendarGrid.MarginLeft -ne '60' -or $calendarGrid.MarginTop -ne '100') {
    throw 'World Events layout contract failed (Calendar geometry): weekday labels and day cells must follow the authored 60-pixel column origin and 148-pixel row origin without a nested pager inset.'
}
Assert-Contains $xmlText 'SuggestedWidth="819" SuggestedHeight="26" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="60" MarginTop="16"' 'Campaign Calendar title centered below the engraved top border'
if ($null -ne $calendarGrid.Sprite -or $null -ne $calendarGrid.Color) {
    throw 'World Events layout contract failed (Calendar design precedence): the live grid still paints over the authored calendar texture.'
}
$calendarTodayOutline = $calendarGrid.SelectSingleNode('.//*[@Id="CalendarTodayOutline"]')
if ($null -eq $calendarTodayOutline -or $null -ne $calendarGrid.SelectSingleNode('.//*[@IsVisible="@IsToday" and @Sprite="frame_9"]')) {
    throw 'World Events layout contract failed (Calendar design precedence): today must use the authored full-cell outline, not legacy popup chrome.'
}
$legacyCalendarPagerButtons = @($calendarPager.SelectNodes('.//ButtonWidget[@Sprite="StdAssets\Popup\button_default"]'))
if ($legacyCalendarPagerButtons.Count -ne 0) {
    throw 'World Events layout contract failed (Calendar design precedence): legacy popup chrome still paints over the authored month pager.'
}
Assert-Contains $viewModelText '#75562EAA' 'selected calendar cell tint'
Assert-Contains $viewModelText '#FFFFFF00' 'transparent unselected calendar cells that reveal the authored cabinet'
Assert-Contains $xmlText 'Id="CalendarSelectedFill" IsVisible="@IsSelected"' 'selected calendar fill uses stable visibility binding'
Assert-NotContains $xmlText 'Color="@BackgroundColor"' 'no recycled dynamic color binding on repeated calendar cells'
Assert-Contains $viewModelText 'foreach (CalendarWorldCalendarDayVM entry in _days)' 'calendar selection updates only the 42 visible cells'
$calendarNotesPanel = $xml.SelectSingleNode('//*[@Id="CalendarEditableNotesPanel"]')
if ($null -eq $calendarNotesPanel -or $null -ne $calendarNotesPanel.SelectSingleNode('.//TextWidget[@Text="NOTES"]')) {
    throw 'World Events layout contract failed (Calendar Day Record): a hard-coded NOTES label duplicates the live selected-date title.'
}
$liveCalendarNotesTitle = $calendarNotesPanel.SelectSingleNode('.//TextWidget[@Text="@NotesTitle"]')
if ($null -eq $liveCalendarNotesTitle -or $liveCalendarNotesTitle.MarginTop -ne '61' -or
    $liveCalendarNotesTitle.SuggestedWidth -ne '122') {
    throw 'World Events layout contract failed (Calendar Day Record): live selected-date title is not registered inside the medallion.'
}
$archivePanel = $xml.SelectSingleNode('//*[@Id="SavedSummariesPanel"]')
$archiveRowArt = $archivePanel.SelectSingleNode('.//*[@Id="SavedSummariesList"]/ItemTemplate/ListPanel/Children/Widget[@Id="SavedSummaryRow"]/Children/TextureWidget[@Id="SavedSummaryRowArt"]')
if ($null -eq $archiveRowArt) {
    throw 'World Events layout contract failed (Archive interaction): record artwork is not inside the repeated live row.'
}
$storyPanel = $xml.SelectSingleNode('//*[@Id="CharacterStoryPanel"]')
$storyScroller = $storyPanel.SelectSingleNode('.//*[@Id="CharacterStoryScroller"]')
$storyBody = $storyPanel.SelectSingleNode('.//*[@Id="CharacterStoryBody"]')
$storyScrollbar = $storyPanel.SelectSingleNode('.//*[@Id="CharacterStoryScrollbar"]')
if ($null -eq $storyScroller -or $storyScroller.SuggestedWidth -ne '535' -or $storyScroller.SuggestedHeight -ne '112' -or
    $storyScroller.MarginRight -ne '106' -or $storyScroller.MarginTop -ne '164' -or $storyScroller.MouseScrollAxis -ne 'Vertical' -or
    $null -eq $storyBody -or $storyBody.'Brush.FontSize' -ne '17' -or $storyBody.'Brush.FontColor' -ne '#000000FF' -or
    $null -eq $storyScrollbar -or $storyScrollbar.IsVisible -ne 'false') {
    throw 'World Events layout contract failed (Personal Chronicle): readable live copy does not fit the approved parchment aperture.'
}
Assert-Contains $xmlText 'Id="MarriageNameSearch" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="307" SuggestedHeight="42" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="96" MarginTop="200"' 'Marriage toolbar aligned to the authored controls'
Assert-Contains $xmlText 'Id="MarriageSortControls" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="584" SuggestedHeight="42" HorizontalAlignment="Right" VerticalAlignment="Top" MarginRight="110" MarginTop="200"' 'Marriage sort controls share the authored toolbar baseline'
Assert-Contains $xmlText 'SuggestedHeight="28" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="8" Brush="Popup.Button.Text" Brush.FontSize="22" Text="MARRIAGE COURT"' 'Marriage title clears the top cabinet border'
Assert-Contains $xmlText 'SuggestedWidth="860" SuggestedHeight="24" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="174" Brush="Popup.Button.Text" Brush.FontSize="14" Text="@MarriageStatusText"' 'Marriage candidate count has a dedicated readable lane above the filters'
Assert-Contains $xmlText 'Id="MarriagePlayerPortraitAperture" DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="92" SuggestedHeight="104" HorizontalAlignment="Left" VerticalAlignment="Center" MarginLeft="40"' 'Marriage player portrait stays inset inside its authored aperture'
Assert-Contains $xmlText 'Id="MarriageCandidatePortraitAperture" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="79" SuggestedHeight="78" HorizontalAlignment="Left" VerticalAlignment="Center" MarginLeft="25"' 'Marriage candidate portraits match their authored 79-by-78 apertures'
$marriageCandidatePortrait = $xml.SelectSingleNode('//*[@Id="MarriageCandidatePortraitAperture"]/Children/ImageIdentifierWidget')
if ($marriageCandidatePortrait.IsBig -eq 'true' -or $marriageCandidatePortrait.ImageTypeCode -ne '@ImageTypeCode') {
    throw 'World Events layout contract failed (Marriage portrait stability): small scrolling portraits must use Bannerlord''s typed non-big image path.'
}
Assert-Contains $xmlText 'Id="MarriageCandidatesScroller" IsVisible="@HasMarriageCandidates" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="1000" SuggestedHeight="321" HorizontalAlignment="Center" VerticalAlignment="Bottom" MarginBottom="14"' 'Marriage candidate row stride remains aligned with the three authored lanes'
Assert-Contains $xmlText 'Id="MarriageCandidatesBackdrop" DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="1000" SuggestedHeight="321" HorizontalAlignment="Center" VerticalAlignment="Bottom" MarginBottom="14"' 'Marriage scrolling viewport masks the obsolete fixed candidate lanes'
Assert-Contains $xmlText 'Id="MarriageCandidateRowArt" DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" TextureProviderName="WorldEventsMarriageRowTextureProvider"' 'Marriage candidate artwork moves with every scrolling row'
Assert-Contains $providerText 'class WorldEventsMarriageRowTextureProvider' 'Marriage candidate rows have a dedicated authored texture provider'
$marriageRowAsset = Join-Path $PSScriptRoot '..\GUI\CustomUI\WorldEventsSkin\page_cabinet_marriage_row_v1.png'
if (-not (Test-Path -LiteralPath $marriageRowAsset)) {
    throw 'World Events layout contract failed (Marriage scrolling): authored moving-row texture is missing.'
}
Assert-Contains $xmlText 'SuggestedWidth="500" SuggestedHeight="24" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="116" MarginTop="6" Brush="Popup.Button.Text" Brush.FontSize="20" Text="@Name"' 'Marriage candidate names clear the portrait frame and share the portrait top baseline'
Assert-Contains $xmlText 'SuggestedWidth="540" SuggestedHeight="18" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="116" MarginTop="74" Brush="Popup.Button.Text" Brush.FontSize="13" Text="@EligibilityText"' 'Marriage candidate text stack stays inside each authored lane'
$marriagePanel = $xml.SelectSingleNode('//*[@Id="MarriagesPanel"]')
$marriageCandidateTemplate = $marriagePanel.SelectSingleNode('.//*[@Id="MarriageCandidatesList"]/ItemTemplate/Widget')
$marriageCandidateRowArt = $marriageCandidateTemplate.SelectSingleNode('./Children/TextureWidget[@Id="MarriageCandidateRowArt"]')
if ($null -eq $marriageCandidateRowArt) {
    throw 'World Events layout contract failed (Marriage scrolling): candidate art is not inside the repeated row template.'
}
$legacyMarriageButtons = @($marriagePanel.SelectNodes('.//ButtonWidget[@Sprite="StdAssets\Popup\button_default"]'))
if ($legacyMarriageButtons.Count -ne 0) {
    throw 'World Events layout contract failed (Marriage design precedence): legacy popup button chrome still paints over the authored cabinet.'
}
$visibleMarriageGoldFrames = @($marriagePanel.SelectNodes('.//BrushWidget[@Brush="TownManagement.GovernorPopup.GoldFrame" and not(@IsVisible="false")]'))
if ($visibleMarriageGoldFrames.Count -ne 0) {
    throw 'World Events layout contract failed (Marriage design precedence): a live gold frame still competes with authored marriage artwork.'
}
Assert-Contains $xmlText 'Text="@DiplomacyText"' 'Diplomacy supporting status binding remains available'
$diplomacySupportingText = $xml.SelectSingleNode('//TextWidget[@Text="@DiplomacyText"]')
if ($diplomacySupportingText.IsVisible -ne 'false') {
    throw 'World Events layout contract failed (Diplomacy geometry): the duplicate status line must not compete with the authored ribbon.'
}
Assert-Contains $xmlText 'SuggestedWidth="430" SuggestedHeight="34" HorizontalAlignment="Left" VerticalAlignment="Top" MarginLeft="70" MarginTop="18" Brush="Popup.Button.Text" Brush.FontSize="18" Text="DIPLOMATIC CORRESPONDENCE"' 'Diplomacy title uses the approved left header slot'
Assert-Contains $xmlText 'SuggestedWidth="430" SuggestedHeight="34" HorizontalAlignment="Right" VerticalAlignment="Top" MarginRight="70" MarginTop="18" Brush="Popup.Button.Text" Brush.FontSize="11" Text="FOREIGN COURTS, ACTIVE WARS, AND ROYAL MESSENGERS"' 'Diplomacy subtitle uses the approved right header slot'
Assert-Contains $xmlText 'SuggestedWidth="1050" SuggestedHeight="22" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="156"' 'Diplomacy realm status stays above the ornamental divider'

$designForegrounds = @{
    Story = 'design_foreground_story_v1.png'
    Marriage = 'design_foreground_marriage_v1.png'
    Diplomacy = 'design_foreground_diplomacy_v1.png'
    Calendar = 'design_foreground_calendar_v1.png'
    Summaries = 'design_foreground_summaries_v1.png'
    Finance = 'design_foreground_finance_v1.png'
}
foreach ($design in $designForegrounds.GetEnumerator()) {
    $designPath = Join-Path $PSScriptRoot ("..\GUI\CustomUI\WorldEventsSkin\" + $design.Value)
    if (-not (Test-Path -LiteralPath $designPath)) {
        throw "World Events layout contract failed (design-first $($design.Key)): missing $designPath"
    }
}
Assert-Contains $xmlText 'Id="CalendarDesignForeground" IsVisible="@IsCalendarVisible"' 'Calendar generated design is the foreground content surface'
Assert-Contains $xmlText 'Id="SummariesDesignForeground"' 'Summaries generated design is the foreground content surface'
Assert-Contains $xmlText 'Id="MarriageDesignForeground"' 'Marriage generated design is the foreground content surface'
Assert-Contains $xmlText 'Id="StoryDesignForeground"' 'Personal Chronicle generated design is the foreground content surface'
Assert-Contains $xmlText 'Id="FinanceDesignForeground" IsVisible="@IsKingdomFinancesPage"' 'Royal Treasury generated design is the foreground content surface'
Assert-Contains $xmlText 'Id="DiplomacyDesignForeground" IsVisible="@IsDiplomacyRelationsPage"' 'Diplomacy generated design is the foreground content surface'
Assert-NotContains $xmlText 'Id="StrategicMapDesignForeground"' 'Strategic Map remains live instead of receiving a static design foreground'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_story_v1"; } }' 'Personal Chronicle design provider'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_marriage_v1"; } }' 'Marriage design provider'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_diplomacy_v1"; } }' 'Diplomacy design provider'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_calendar_v1"; } }' 'Calendar design provider'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_summaries_v1"; } }' 'Summaries design provider'
Assert-Contains $providerText 'AssetName { get { return "design_foreground_finance_v1"; } }' 'Royal Treasury design provider'

if ($xmlText.Contains('SuggestedHeight="76" HorizontalAlignment="Center" VerticalAlignment="Top" Sprite="BlankWhiteSquare" Color="#081015D8"')) {
    throw 'World Events layout contract failed (Saved Summaries visual language): a standard content tab still has a separate header slab.'
}

foreach ($panelId in 'SavedSummariesPanel', 'CharacterStoryPanel', 'CompanionsPanel', 'DiplomacyPanel', 'WarStatisticsPanel') {
    $opaquePanelPattern = 'Id="' + $panelId + '"' + '[^>]*Sprite="BlankWhiteSquare"'
    if ($xmlText -match $opaquePanelPattern) {
        throw "World Events layout contract failed (integrated $panelId): the tab still paints a modal background over the shared cabinet."
    }
}

Assert-Contains $providerText 'AssetName { get { return "foreground_diplomacy"; } }' 'restored Diplomacy selected foreground sprite'
Assert-NotContains $providerText 'WorldEventsForeignOfficeInactiveTextureProvider' 'Foreign Office inactive overlay provider removed'
Assert-NotContains $xmlText 'Sprite="StdAssets\Popup\canvas" Color="#E7C58DFF"' 'obsolete native popup canvas removed from custom shell'
Assert-NotContains $xmlText 'Sprite="frame_9" ExtendLeft="20" ExtendTop="20" ExtendRight="20" ExtendBottom="20" Color="#B58A4CFF"' 'obsolete native outer frame removed from custom shell'
Assert-Contains $screenText 'private const float WindowScale = 0.90f;' 'non-overlapping window scale'
Assert-Contains $screenText '_layer.UIContext.ScaleModifier = _layer.Scale * WindowScale;' 'uniform Gauntlet context scaling'
Assert-Contains $screenText 'public override void UpdateLayout()' 'resolution-change scale restoration'

$expectedShellHash = '30CBA8AFFC3C5220DFD8346A8129EC49AF7C4BD72D7C9BD56AB10AA866B30C44'
$actualShellHash = (Get-FileHash -LiteralPath $ShellPath -Algorithm SHA256).Hash
if ($actualShellHash -ne $expectedShellHash) {
    throw "World Events shell artwork hash mismatch: expected $expectedShellHash, got $actualShellHash."
}

foreach ($tab in 'Realm Chronicle', 'My Story', 'Realm Affairs', 'Military Affairs') {
    # The ViewModel still drives hit targets and selection while normal captions are baked into the shell.
    Assert-Contains $xmlText 'DataSource="{Tabs}"' "data-bound $tab tab"
}

Write-Host 'World Events layout verification passed.'
