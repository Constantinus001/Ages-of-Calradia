param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'

[xml]$xml = Get-Content -LiteralPath (Join-Path $Root 'GUI\Prefabs\WorldCalendar\WorldCalendar.xml') -Raw
$vm = Get-Content -LiteralPath (Join-Path $Root 'CalendarWorldLedgerVM.cs') -Raw

function Require-Node([string]$id) {
    $node = $xml.SelectSingleNode("//*[@Id='$id']")
    if ($null -eq $node) { throw "Strategic Map contract failed: missing $id." }
    return $node
}

$art = Require-Node 'StrategicPageCabinetArt'
$map = Require-Node 'StrategicMapPanel'
$scroller = Require-Node 'StrategicMapScroller'
$summary = Require-Node 'StrategicSummaryScroller'
$summaryBody = Require-Node 'StrategicSummaryBody'
$summaryScrollbar = Require-Node 'StrategicSummaryScrollbar'
$summaryScrollbarHandle = Require-Node 'StrategicSummaryScrollbarHandle'
$mapBorder = Require-Node 'StrategicMapLiveBorder'
$zoomConsole = Require-Node 'StrategicMapZoomConsole'
$zoomInputShield = Require-Node 'StrategicMapZoomInputShield'
$summaryHeading = Require-Node 'StrategicSummaryHeading'
$kingdomCards = Require-Node 'StrategicKingdomCardList'
$foreground = Require-Node 'StrategicExactCabinetForeground'

if ($art.SuggestedWidth -ne '1115' -or $art.SuggestedHeight -ne '514' -or
    $map.SuggestedWidth -ne '741' -or $map.SuggestedHeight -ne '427' -or
    $map.MarginLeft -ne '87' -or $map.MarginTop -ne '305') {
    throw 'Strategic Map design failed: live atlas no longer occupies the approved cabinet aperture.'
}
if ($scroller.LocalName -ne 'StrategicMapZoomScrollablePanel') {
    throw 'Strategic Map interaction failed: custom pan/zoom panel is not active.'
}
if ($mapBorder.IsVisible -ne 'false' -or $zoomConsole.IsVisible -ne 'false' -or
    $foreground.TextureProviderName -ne 'WorldEventsStrategicForegroundTextureProvider' -or
    $foreground.'DoNotAcceptEvents' -ne 'true' -or
    $foreground.SuggestedWidth -ne '1115' -or $foreground.SuggestedHeight -ne '514' -or
    $null -ne $xml.SelectSingleNode('//*[@Id="StrategicDesignForeground"]')) {
    throw 'Strategic Map design failed: exact editable cabinet foreground is not active.'
}

$controls = @(
    @{ Id='StrategicMapResetButton'; Command='ExecuteResetMapView' },
    @{ Id='StrategicMapZoomOutButton'; Command='ExecuteZoomOut' },
    @{ Id='StrategicMapZoomInButton'; Command='ExecuteZoomIn' },
    @{ Id='StrategicBackToKingdomButton'; Command='ExecuteShowKingdomSummary' },
    @{ Id='StrategicTrackSettlementButton'; Command='ExecuteTrackSelectedSettlement' }
)
foreach ($control in $controls) {
    $node = Require-Node $control.Id
    if ($node.'Command.Click' -ne $control.Command -or
        -not $vm.Contains("public void $($control.Command)")) {
        throw "Strategic Map interaction failed: $($control.Id) is not bound to a live VM command."
    }
}

if ($summary.LocalName -ne 'WorldEventsRowSnapScrollablePanel' -or
    $summary.ResetOnShow -ne 'true' -or
    $summary.RowStride -ne '119' -or
    $summary.WidthSizePolicy -ne 'Fixed' -or $summary.SuggestedWidth -ne '285' -or
    $summary.HeightSizePolicy -ne 'StretchToParent' -or
    $summary.MarginTop -ne '@StrategicSummaryContentTop' -or
    $summary.MarginBottom -ne '@StrategicSummaryScrollerMarginBottom' -or
    $summaryBody.SuggestedWidth -ne '248' -or $summaryBody.MarginLeft -ne '10' -or
    $summaryScrollbar.HeightSizePolicy -ne 'StretchToParent' -or
    $summaryScrollbar.MarginTop -ne '@StrategicSummaryContentTop' -or
    $summaryScrollbar.MarginBottom -ne '@StrategicSummaryScrollerMarginBottom') {
    throw 'Strategic Map ledger failed: summary text and scrollbar do not share the approved lower aperture.'
}
if ($summaryScrollbar.LocalName -ne 'ScrollbarWidget' -or
    $summaryScrollbar.SuggestedWidth -ne '13' -or
    $summaryScrollbar.Handle -ne 'StrategicSummaryScrollbarHandle' -or
    $summaryScrollbarHandle.SuggestedWidth -ne '11' -or
    $summaryScrollbarHandle.SuggestedHeight -ne '42') {
    throw 'Strategic Map ledger failed: the approved thin bronze scrollbar is not active.'
}

$zoomRail = Require-Node 'StrategicMapZoomRailHitArea'
if ($zoomConsole.IsVisible -ne 'false' -or
    $zoomRail.SuggestedWidth -ne '72' -or
    $zoomRail.MarginLeft -ne '90' -or
    $zoomInputShield.SuggestedWidth -ne '220' -or
    $zoomInputShield.SuggestedHeight -ne '39' -or
    $zoomInputShield.MarginRight -ne '5' -or
    $zoomInputShield.MarginBottom -ne '3' -or
    (Require-Node 'StrategicMapResetButton').ParentNode.ParentNode.Id -ne 'StrategicMapZoomInputShield' -or
    (Require-Node 'StrategicMapZoomOutButton').ParentNode.ParentNode.Id -ne 'StrategicMapZoomInputShield' -or
    (Require-Node 'StrategicMapZoomInButton').ParentNode.ParentNode.Id -ne 'StrategicMapZoomInputShield' -or
    (Require-Node 'StrategicMapResetButton').ChildNodes.Count -ne 0 -or
    (Require-Node 'StrategicMapZoomOutButton').ChildNodes.Count -ne 0 -or
    (Require-Node 'StrategicMapZoomInButton').ChildNodes.Count -ne 0) {
    throw 'Strategic Map design failed: duplicate zoom-console art is obscuring the approved cabinet control.'
}

$skinProvider = Get-Content -LiteralPath (Join-Path $Root 'WorldEventsSkinTextureProviders.cs') -Raw
if (-not $skinProvider.Contains('bool zoomConsoleArtwork') -or
    -not $skinProvider.Contains('mapAperture && !zoomConsoleArtwork')) {
    throw 'Strategic Map design failed: the exact cabinet zoom artwork is still cleared by the foreground mask.'
}
if ($summaryHeading.Text -ne 'KINGDOM SUMMARY' -or
    $summaryHeading.'Brush.TextHorizontalAlignment' -ne 'Left') {
    throw 'Strategic Map ledger failed: summary heading is not aligned to the live ledger copy.'
}
if ($summaryHeading.ParentNode.Id -eq 'StrategicSummaryBody' -or
    $kingdomCards.DataSource -ne '{StrategicKingdomRows}' -or
    -not $vm.Contains('public MBBindingList<CalendarStrategicKingdomSummaryVM> StrategicKingdomRows') -or
    -not $vm.Contains('private void RefreshStrategicKingdomRows()')) {
    throw 'Strategic Map ledger failed: fixed heading and structured live kingdom cards are not active.'
}

$cardName = $xml.SelectSingleNode('//*[@Id="StrategicKingdomCardList"]/ItemTemplate/Widget/Children/TextWidget[@Text="@Name"]')
$cardLeader = $xml.SelectSingleNode('//*[@Id="StrategicKingdomCardList"]/ItemTemplate/Widget/Children/TextWidget[@Text="@LeaderText"]')
$cardClan = $xml.SelectSingleNode('//*[@Id="StrategicKingdomCardList"]/ItemTemplate/Widget/Children/TextWidget[@Text="@RulingClanText"]')
$cardStrength = $xml.SelectSingleNode('//*[@Id="StrategicKingdomCardList"]/ItemTemplate/Widget/Children/TextWidget[@Text="@StrengthText"]')
$cardHoldings = $xml.SelectSingleNode('//*[@Id="StrategicKingdomCardList"]/ItemTemplate/Widget/Children/TextWidget[@Text="@HoldingsText"]')
if ($null -eq $cardName -or $cardName.'Brush.FontSize' -lt 19 -or
    $null -eq $cardLeader -or $cardLeader.'Brush.FontSize' -lt 14 -or
    $null -eq $cardClan -or $cardClan.'Brush.FontSize' -lt 14 -or
    $null -eq $cardStrength -or $cardStrength.'Brush.FontSize' -lt 12 -or
    $null -eq $cardHoldings -or $cardHoldings.'Brush.FontSize' -lt 12) {
    throw 'Strategic Map ledger failed: kingdom-card typography is below the measured legibility floor.'
}

$settlementButton = $xml.SelectSingleNode('//*[@Id="StrategicMapCanvas"]//ButtonWidget[@Command.Click="ExecuteSelect"]')
if ($null -eq $settlementButton) {
    throw 'Strategic Map interaction failed: settlement selection buttons are missing.'
}

$settlementLabel = $xml.SelectSingleNode('//*[@Id="StrategicMapCanvas"]//TextWidget[@Text="@Label" and @IsVisible="@ShowLabel"]')
$mapProvider = Get-Content -LiteralPath (Join-Path $Root 'CalendarStrategicMapTextureProvider.cs') -Raw
if ($null -eq $settlementLabel -or
    $settlementLabel.'Brush.FontSize' -lt 12 -or
    $settlementLabel.SuggestedWidth -lt 168 -or
    $vm.Contains('StrategicMapSettlementLabelMinimumZoom') -or
    $vm -notmatch 'iconSize,\s*true,\s*point\.IsUnderSiege' -or
    -not $vm.Contains('public bool ShowLabel') -or
    $mapProvider.Contains('DrawTownLabels(composedMap, markers);')) {
    throw 'Strategic Map city-label contract failed: town names must remain visible and legible at every UI-map zoom.'
}

Write-Host 'Strategic Map design and interaction verification passed.'
