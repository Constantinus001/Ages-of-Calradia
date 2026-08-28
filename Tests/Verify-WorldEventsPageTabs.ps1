param(
    [string]$PrefabPath = (Join-Path $PSScriptRoot '..\GUI\Prefabs\WorldCalendar\WorldCalendar.xml'),
    [string]$ViewModelPath = (Join-Path $PSScriptRoot '..\CalendarWorldLedgerVM.cs')
)

$ErrorActionPreference = 'Stop'

[xml]$prefab = Get-Content -Raw -LiteralPath $PrefabPath
$viewModel = Get-Content -Raw -LiteralPath $ViewModelPath

$pageCommands = @(
    'ExecuteShowCalendarPage',
    'ExecuteShowSavedSummaries',
    'ExecuteShowCharacterStory',
    'ExecuteShowCompanionsPage',
    'ExecuteShowKingdomFinances',
    'ExecuteShowDiplomacyRelations',
    'ExecuteShowMarriagesPage',
    'ExecuteShowStrategicMapPage',
    'ExecuteShowStrategicWarStatistics'
)

foreach ($command in $pageCommands) {
    $buttons = @($prefab.SelectNodes("//ButtonWidget[@*[name()='Command.Click' and .='$command']]"))
    if ($buttons.Count -ne 1) {
        throw "Page-tab contract failed: expected one live $command button, found $($buttons.Count)."
    }

    $button = $buttons[0]
    if ($button.DoNotPassEventsToChildren -ne 'true') {
        throw "Page-tab contract failed: $command does not own its click events."
    }

    if (-not $viewModel.Contains("public void $command()")) {
        throw "Page-tab contract failed: $command has no ViewModel handler."
    }
}

$mainTabButton = $prefab.SelectSingleNode('//ListPanel[@DataSource="{Tabs}"]/ItemTemplate/ButtonWidget[@Command.Click="ExecuteSelect"]')
if ($null -eq $mainTabButton -or $mainTabButton.DoNotPassEventsToChildren -ne 'true') {
    throw 'Page-tab contract failed: the four main-tab slots do not own ExecuteSelect clicks.'
}

if (-not $viewModel.Contains('public void ExecuteSelect() { if (_select != null) _select(this); }')) {
    throw 'Page-tab contract failed: main-tab ExecuteSelect does not dispatch to the selected tab.'
}

# The global screen refreshes once per second. Chronicle records must survive
# that refresh instead of being cleared back to their collapsed constructor
# state, and unchanged data must not reset the archive scroller.
foreach ($archiveRefreshContract in @(
    'Dictionary<string, bool> expandedByTitle',
    'expandedByTitle[GetSavedSummaryIdentity(existing.Title)] = true;',
    'if (!changed) return;',
    'expandedByTitle.ContainsKey(GetSavedSummaryIdentity(monthTitle))'
)) {
    if (-not $viewModel.Contains($archiveRefreshContract)) {
        throw "Chronicle refresh contract failed: missing '$archiveRefreshContract'."
    }
}

foreach ($foregroundId in @(
    'CalendarDesignForeground',
    'SummariesDesignForeground',
    'MarriageDesignForeground',
    'StoryDesignForeground',
    'FinanceDesignForeground',
    'DiplomacyDesignForeground'
)) {
    $foreground = $prefab.SelectSingleNode("//*[@Id='$foregroundId']")
    if ($null -eq $foreground -or $foreground.DoNotAcceptEvents -ne 'true') {
        throw "Page-tab contract failed: $foregroundId can intercept page-control clicks."
    }
    if ($foreground.GetAttribute('IsVisible') -ne 'false') {
        throw "Editable-page contract failed: frozen $foregroundId must remain disabled."
    }
}

# Opaque mockups are retained only as dormant source assets. Their active
# counterparts are blank ornamental cabinet textures and data-bound widgets.

$realmEditable = $prefab.SelectSingleNode("//*[@Id='RealmAffairsEditableLayer']")
if ($null -eq $realmEditable -or $realmEditable.IsVisible -ne 'true') {
    throw 'Realm Affairs contract failed: native editable ledger layer is not active.'
}
$financeLedger = $realmEditable.SelectSingleNode(".//*[@Id='KingdomFinanceScroller']")
if ($null -eq $financeLedger -or $financeLedger.IsVisible -ne '@IsKingdomFinanceLedgerVisible' -or $financeLedger.MouseScrollAxis -ne 'Vertical') {
    throw 'Realm Affairs contract failed: Kingdom Finances has no live, scrollable ledger.'
}
foreach ($field in @('KingdomTreasuryText', 'KingdomIncomeText', 'KingdomExpensesText', 'KingdomNetText', 'ForeignOfficePeaceText', 'ForeignOfficeWarText', 'ForeignOfficeIncomeText')) {
    if ($null -eq $realmEditable.SelectSingleNode(".//*[@Text='@$field']")) {
        throw "Realm Affairs contract failed: native ledger does not bind $field."
    }
}
$diplomacyLedger = $realmEditable.SelectSingleNode(".//*[@Id='DiplomacyScroller']")
$diplomacyRows = $realmEditable.SelectSingleNode(".//*[@Id='DiplomacyBody']")
if ($null -eq $diplomacyLedger -or $null -eq $diplomacyRows -or $diplomacyRows.DataSource -ne '{DiplomacyRelations}') {
    throw 'Realm Affairs contract failed: native diplomacy ledger does not use live kingdom relations.'
}
$marriageLedger = $prefab.SelectSingleNode("//*[@Id='MarriageCandidatesList']")
if ($null -eq $marriageLedger -or $marriageLedger.DataSource -ne '{MarriageCandidates}') {
    throw 'Realm Affairs contract failed: native marriage ledger does not use live marriage candidates.'
}

$requiredStateAssignments = @{
    ExecuteShowCalendarPage = 'IsCalendarVisible = true;'
    ExecuteShowSavedSummaries = 'IsSummariesVisible = true;'
    ExecuteShowCharacterStory = 'IsCharacterStoryVisible = true;'
    ExecuteShowCompanionsPage = 'IsCompanionsVisible = true;'
    ExecuteShowKingdomFinances = 'IsKingdomFinancesPage = true;'
    ExecuteShowDiplomacyRelations = 'IsDiplomacyRelationsPage = true;'
    ExecuteShowMarriagesPage = 'IsMarriagesPage = true;'
    ExecuteShowStrategicMapPage = 'IsStrategicMap = true;'
    ExecuteShowStrategicWarStatistics = 'IsWarStatisticsVisible = true;'
}

foreach ($entry in $requiredStateAssignments.GetEnumerator()) {
    if (-not $viewModel.Contains($entry.Value)) {
        throw "Page-tab contract failed: $($entry.Key) does not activate $($entry.Value)"
    }
}

# Each page-tab state must reveal a concrete content panel. This prevents a
# handler from changing a boolean that no longer controls any live page.
$pagePanels = @{
    IsCalendarVisible = 'CalendarEditableContentPanel'
    IsSavedSummariesPage = 'SavedSummariesPanel'
    IsCharacterStoryVisible = 'CharacterStoryPanel'
    IsCompanionsVisible = 'CompanionsPanel'
    IsDiplomacyRelationsPage = 'DiplomacyScroller'
    IsMarriagesPage = 'MarriagesPanel'
    IsStrategicMap = 'StrategicMapPanel'
    IsWarStatisticsVisible = 'WarStatisticsPanel'
}

foreach ($entry in $pagePanels.GetEnumerator()) {
    $panel = $prefab.SelectSingleNode("//*[@Id='$($entry.Value)']")
    if ($null -eq $panel) {
        throw "Page-tab contract failed: missing live panel $($entry.Value)."
    }

    $visibility = $panel.GetAttribute('IsVisible')
    if ($visibility -ne "@$($entry.Key)" -and $entry.Value -ne 'RealmAffairsPanel') {
        throw "Page-tab contract failed: $($entry.Value) is not controlled by @$($entry.Key)."
    }
}

$realmPanel = $prefab.SelectSingleNode("//*[@Id='DiplomacyPanel']")
if ($null -eq $realmPanel -or $realmPanel.IsVisible -ne '@IsRealmLedgerVisible') {
    throw 'Page-tab contract failed: the Realm Affairs page container is not controlled by its visible-state binding.'
}

# Calendar, story, marriage, and Realm Affairs were rebuilt using blank cabinet
# artwork plus live native controls. Their previous opaque foreground layers
# stay disabled; there is no legacy duplicate tree to assert here.

# Every clickable control in the prefab must retain an implemented handler. This
# covers the page tabs above as well as the calendar, summaries, marriage,
# diplomacy, war, and strategic-map controls that remain visible on those pages.
$commandButtons = @($prefab.SelectNodes("//ButtonWidget[@*[name()='Command.Click']]"))
if ($commandButtons.Count -eq 0) {
    throw 'Interaction contract failed: no clickable World Events controls were found.'
}

foreach ($button in $commandButtons) {
    $command = $button.GetAttribute('Command.Click')
    if ([string]::IsNullOrWhiteSpace($command)) {
        throw 'Interaction contract failed: a button contains an empty Command.Click binding.'
    }

    if ($button.DoNotPassEventsToChildren -ne 'true') {
        throw "Interaction contract failed: $command can lose clicks to one of its child widgets."
    }

    if ($command -ne 'ExecuteSelect' -and -not $viewModel.Contains("public void $command(")) {
        throw "Interaction contract failed: $command has no public ViewModel handler."
    }

    # A zero-alpha Gauntlet button can be skipped by hit testing. The sole
    # global close target is intentionally invisible; every page control must
    # stay hit-testable at its foreground-aligned location.
    if ($button.GetAttribute('AlphaFactor') -eq '0') {
        throw "Interaction contract failed: $($button.Id) uses a zero-alpha clickable hit target."
    }
}

$mapBarPrefabPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'GUI\Prefabs\Map\MapBar.xml'
[xml]$mapBarPrefab = Get-Content -LiteralPath $mapBarPrefabPath -Raw
$openToggle = $mapBarPrefab.SelectSingleNode("//*[@Id='WorldCalendarButton']")
$closeToggle = $prefab.SelectSingleNode("//*[@Id='WorldCalendarOpenOverlayToggle']")
if ($null -eq $openToggle -or $openToggle.GetAttribute('Command.Click') -ne 'ExecuteToggleWorldCalendar') {
    throw 'W-toggle contract failed: the map-bar W button no longer opens World Events.'
}
if ($null -eq $closeToggle -or $closeToggle.GetAttribute('Command.Click') -ne 'ExecuteClose') {
    throw 'W-toggle contract failed: the open-overlay W hitbox no longer closes World Events.'
}
if ($closeToggle.SuggestedWidth -ne '34' -or $closeToggle.SuggestedHeight -ne '31' -or
    $closeToggle.PositionXOffset -ne '218' -or $closeToggle.MarginBottom -ne '2') {
    throw 'W-toggle contract failed: the close hitbox is not aligned with the native map-bar W button.'
}

$requiredActionCommands = @(
    'ExecutePreviousCalendarMonth', 'ExecuteNextCalendarMonth', 'ExecuteToggle',
    'ExecuteSortMarriagesByName', 'ExecuteSortMarriagesByAge',
    'ExecuteSortMarriagesByKingdom', 'ExecuteSortMarriagesByAll',
    'ExecuteContactCandidate', 'ExecuteContactClanLeader', 'ExecuteSendMessenger',
    'ExecuteResolveWar', 'ExecuteZoomIn', 'ExecuteZoomOut', 'ExecuteResetMapView',
    'ExecuteToggleTrack', 'ExecuteShowKingdomSummary', 'ExecuteTrackSelectedSettlement',
    'ExecuteClose'
)

foreach ($command in $requiredActionCommands) {
    if (@($commandButtons | Where-Object { $_.GetAttribute('Command.Click') -eq $command }).Count -eq 0) {
        throw "Interaction contract failed: expected visible action $command is absent."
    }
}

foreach ($command in @('ExecuteContactCandidate', 'ExecuteContactClanLeader', 'ExecuteSendMessenger', 'ExecuteResolveWar')) {
    if (@($commandButtons | Where-Object { $_.GetAttribute('Command.Click') -eq $command }).Count -eq 0) {
        throw "Realm Affairs contract failed: expected live action $command is absent."
    }
}

$search = $prefab.SelectSingleNode("//*[@Id='MarriageNameSearchInput']")
if ($null -eq $search -or $search.Text -ne '@MarriageNameSearchText') {
    throw 'Interaction contract failed: marriage search is no longer bound to the live candidate filter.'
}

if (-not $viewModel.Contains('RefreshMarriages();')) {
    throw 'Interaction contract failed: changing the marriage search text no longer refreshes candidates.'
}

if (-not $viewModel.Contains('_sendMessenger(_clanLeader, "ClanLeader")')) {
    throw 'Interaction contract failed: the clan-leader court button does not dispatch to its clan leader.'
}

$dayGrid = $prefab.SelectSingleNode("//*[@Id='CalendarEditableDayGrid']")
if ($null -eq $dayGrid -or $dayGrid.DataSource -ne '{Days}' -or $dayGrid.DefaultCellWidth -ne '102' -or $dayGrid.DefaultCellHeight -ne '63') {
    throw 'Interaction contract failed: the calendar date grid no longer owns its exact live-day geometry.'
}

$dayButton = $dayGrid.SelectSingleNode('.//ButtonWidget[@Command.Click="ExecuteSelect"]')
if ($null -eq $dayButton -or $dayButton.WidthSizePolicy -ne 'StretchToParent' -or $dayButton.HeightSizePolicy -ne 'StretchToParent') {
    throw 'Interaction contract failed: calendar days do not fill their clickable grid cells.'
}

$dayRecord = $prefab.SelectSingleNode("//*[@Id='CalendarEditableNotesPanel']")
if ($null -eq $dayRecord -or $dayRecord.IsVisible -ne '@IsCalendarVisible' -or $null -eq $dayRecord.SelectSingleNode(".//*[@Text='@NotesText']")) {
    throw 'Interaction contract failed: a selected calendar date has no visible editable notes readout.'
}

if (-not $viewModel.Contains('OnPropertyChangedWithValue(HasSelectedCalendarDay, "HasSelectedCalendarDay")')) {
    throw 'Interaction contract failed: a calendar-date click does not refresh its foreground readout.'
}

if (-not $viewModel.Contains('_selectedCalendarDay = long.MinValue;') -or -not $viewModel.Contains('RefreshCalendarNotes();')) {
    throw 'Interaction contract failed: calendar month navigation can leave a stale selected-day record visible.'
}

if (-not $viewModel.Contains('Calendar month navigation ignored: direction=') -or -not $viewModel.Contains('Calendar month displayed: year=')) {
    throw 'Interaction contract failed: calendar month clicks no longer emit actionable runtime diagnostics.'
}

# Editable calendar cells must display their real campaign values rather than
# static numbers printed into an opaque foreground image.
$calendarDayNumber = $dayGrid.SelectSingleNode(".//TextWidget[@Text='@DayNumber']")
$calendarDayEvent = $dayGrid.SelectSingleNode(".//TextWidget[@Text='@EventSummary']")
if ($null -eq $calendarDayNumber -or $null -eq $calendarDayEvent) {
    throw 'Editable calendar contract failed: calendar does not render live day numbers and event summaries.'
}
if ($calendarDayEvent.Id -ne 'CalendarDayEventSummary' -or
    $calendarDayEvent.'Brush.FontSize' -ne '14' -or
    $calendarDayEvent.Color -ne '#FFF0C9FF' -or
    $calendarDayEvent.SuggestedHeight -ne '34') {
    throw 'Editable calendar contract failed: calendar event summaries are not using the legible lower-cell lane.'
}

$summaryOpenRecord = $prefab.SelectSingleNode("//*[@Id='SummaryDesignOpenRecord']")
if ($null -eq $summaryOpenRecord -or
    $null -eq $summaryOpenRecord.SelectSingleNode(".//TextWidget[@Text='@Title']") -or
    $null -eq $summaryOpenRecord.SelectSingleNode(".//TextWidget[@Text='@ActionText']") -or
    $null -eq $summaryOpenRecord.SelectSingleNode(".//TextWidget[@Text='@ExpandGlyph']") -or
    @($summaryOpenRecord.SelectNodes(".//Widget[@Sprite='BlankWhiteSquare_9' and @Color='#17120EFA']")).Count -lt 2) {
    throw 'Functional-design contract failed: summary cards lack a masked live title/action aperture.'
}

$summaryExpanded = $prefab.SelectSingleNode("//*[@Id='SummaryDesignExpandedRecord']")
if ($null -eq $summaryExpanded -or $null -eq $summaryExpanded.SelectSingleNode(".//TextWidget[@Text='@SummaryText']")) {
    throw 'Functional-design contract failed: expanded summary cards do not render their live record text.'
}

$storyLayer = $prefab.SelectSingleNode("//*[@Id='StoryEditableLayer' and @IsVisible='true']")
if ($null -eq $storyLayer) {
    throw 'Functional-design contract failed: the editable story cabinet is not active.'
}

foreach ($binding in @('@CharacterStoryTitle', '@CharacterStorySubtitle', '@CharacterKillsText', '@CharacterKnockoutsText', '@CharacterRecordNote')) {
    if ($null -eq $storyLayer.SelectSingleNode(".//TextWidget[@Text='$binding']")) {
        throw "Functional-design contract failed: editable story cabinet does not bind $binding."
    }
}

$storyPortrait = $storyLayer.SelectSingleNode(".//ImageIdentifierWidget[@DataSource='{CharacterPortrait}']")
$storyScroller = $storyLayer.SelectSingleNode(".//*[@Id='CharacterStoryScroller']")
$storyBody = $storyLayer.SelectSingleNode(".//*[@Id='CharacterStoryBody' and @Text='@CharacterStoryText']")
if ($null -eq $storyPortrait -or $null -eq $storyScroller -or $storyScroller.MouseScrollAxis -ne 'Vertical' -or $null -eq $storyBody) {
    throw 'Functional-design contract failed: editable story portrait or scrollable campaign narrative is missing.'
}

Write-Host 'World Events page-tab interaction verification passed.'
