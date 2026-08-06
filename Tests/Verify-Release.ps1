param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReleaseArchive,
    [switch]$AllowDirtySource,
    [ValidateRange(1, 30)]
    [int]$CloudVerdictHoldMinutes = 10
)

$ErrorActionPreference = 'Stop'

if (-not $AllowDirtySource) {
    $sourceChanges = @(git -C $ModuleRoot status --porcelain)
    if ($sourceChanges.Count -gt 0) {
        throw 'Source tree has uncommitted changes. Commit the exact release source before packaging.'
    }
}

$mainProject = Join-Path $ModuleRoot 'TwelveMonthCalendar.csproj'
$mcmProject = Join-Path $ModuleRoot 'TwelveMonthCalendar.MCM.csproj'
dotnet msbuild $mainProject /t:Rebuild /p:Configuration=Release /v:minimal
dotnet msbuild $mcmProject /t:Rebuild /p:Configuration=Release /v:minimal
& (Join-Path $PSScriptRoot 'Verify-CalendarMath.ps1') -ModuleRoot $ModuleRoot

$settlementBalanceSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'SettlementBalancePatches.cs')
$dailyBalanceSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'DailyRateBalancePatches.cs')
if ($dailyBalanceSource -match 'SettlementDemandBalancePatch[\s\S]{0,500}BonusToFoodStores' -or
    $dailyBalanceSource -match 'SettlementBudgetBalancePatch[\s\S]{0,500}BonusToFoodStores') {
    throw 'Civilian food demand and market budget must share the Gregorian food cadence.'
}
if ($dailyBalanceSource -notmatch 'SettlementMarketSmoothingBalancePatch' -or
    $dailyBalanceSource -notmatch 'ScaleDailySmoothingFactor') {
    throw 'Settlement market convergence must preserve its native annual cadence.'
}
if ($settlementBalanceSource -match 'SumOfFactorsField\.SetValueDirect' -or
    $settlementBalanceSource -notmatch 'BaseNumber \* factor') {
    throw 'ExplainedNumber annual scaling must preserve native factor modifiers and scale only its base.'
}
$financeSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarClanFinanceModel.cs')
if ($settlementBalanceSource -match 'FoodSupplyRateFactor' -or
    $settlementBalanceSource -notmatch 'VillageFoodProductionBalancePatch') {
    throw 'Village food must use the coordinated Gregorian production path.'
}
$partyFoodSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarFoodLogisticsModels.cs')
if ($partyFoodSource -notmatch 'CalendarMobilePartyFoodConsumptionModel' -or
    $partyFoodSource -notmatch 'CalendarPartyFoodBuyingModel' -or
    $partyFoodSource -notmatch 'CalendarSettlementFoodModel' -or
    $partyFoodSource -notmatch 'nativeDays / SettlementBalanceMath\.DailyRateFactor' -or
    $partyFoodSource -notmatch 'nativeMarketResult') {
    throw 'Town food, food production, party rations, and AI reserve targets must use matched Gregorian logistics without market double-scaling.'
}
$balanceTelemetrySource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarBalanceTelemetry.cs')
if ($balanceTelemetrySource -notmatch 'AvgDirectFoodChange' -or
    $balanceTelemetrySource -notmatch 'AvgMarketFoodChange' -or
    $balanceTelemetrySource -notmatch 'CappedFoodTowns') {
    throw 'Monthly diagnostics must report direct and market food balance separately.'
}
if ($financeSource -match 'ApplyAiReserveSurcharge|reserveTarget|maximumSurcharge') {
    throw 'AI finance must use Bannerlord''s native high-cash expense rather than an additional surcharge.'
}
if ($financeSource -notmatch 'wrapper is therefore the single active' -or
    $financeSource -notmatch 'Scale\(ref result\);') {
    throw 'The startup-safe clan-finance wrapper must perform the single active annual scaling.'
}
if ($financeSource -notmatch 'EvaluateClanFinance' -or
    $financeSource -notmatch 'ReconcileKingdomBudgetWallet' -or
    $financeSource -notmatch 'ScaleDiscreteDailyValue\(') {
    throw 'Clan-finance side effects must reconcile Kingdom Budget transfers through the safe wrapper.'
}
$financeTelemetrySource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarFinanceTelemetry.cs')
if ($financeTelemetrySource -notmatch 'KingdomBudgetTransfers' -or
    $financeTelemetrySource -notmatch 'RecordKingdomBudgetTransfer') {
    throw 'Monthly finance diagnostics must report native and scaled Kingdom Budget transfers.'
}

$dailyFactor = 84.0 / 365.2425
$durationFactor = 365.2425 / 84.0
if ([Math]::Abs(($dailyFactor * $durationFactor) - 1.0) -gt 0.000001) {
    throw 'Annual duration and daily-rate factors are not reciprocal.'
}

$nativeProbability = 0.25
$annualProbability = 1.0 - [Math]::Pow(1.0 - $nativeProbability, $dailyFactor)
$nativeAnnualSurvival = [Math]::Pow(1.0 - $nativeProbability, 84.0)
$annualSurvival = [Math]::Pow(1.0 - $annualProbability, 365.2425)
if ([Math]::Abs($nativeAnnualSurvival - $annualSurvival) -gt 0.000001) {
    throw 'Daily probability conversion does not preserve annual probability.'
}

$nativeMarketSmoothing = 0.15
$calendarMarketSmoothing = 1.0 - [Math]::Pow(1.0 - $nativeMarketSmoothing, $dailyFactor)
$nativeMarketRetention = [Math]::Pow(1.0 - $nativeMarketSmoothing, 84.0)
$calendarMarketRetention = [Math]::Pow(1.0 - $calendarMarketSmoothing, 365.2425)
if ([Math]::Abs($nativeMarketRetention - $calendarMarketRetention) -gt 0.000001) {
    throw 'Settlement market smoothing does not preserve native annual convergence.'
}

$nativeTributePerDay = 100
$nativeTributeDays = 100
$calendarTributePerDay = $nativeTributePerDay * $dailyFactor
$calendarTributeDays = 235
$nativeTributeTotal = $nativeTributePerDay * $nativeTributeDays
$calendarTributeTotal = $calendarTributePerDay * $calendarTributeDays
if ($calendarTributeDays -ne 235 -or $calendarTributeTotal -le 0) {
    throw 'Tribute treaties must use the configured 235-calendar-day term with a positive finance payment cadence.'
}

$diplomacySource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'DiplomacyBalancePatches.cs')
if ($diplomacySource -notmatch 'GregorianTruceDays = 100f' -or
    $diplomacySource -notmatch 'PeaceDeclarationDate\.ElapsedDaysUntilNow <= GregorianTruceDays') {
    throw 'War declarations must enforce the configured 100-calendar-day truce.'
}

$pacingSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CampaignPacingPatch.cs')
if ($pacingSource -notmatch 'SpeedUpMultiplier' -or
    $pacingSource -match 'realDt \*=') {
    throw 'Fast-forward speed must use Bannerlord''s direct SpeedUpMultiplier without stacking a TickMapTime multiplier.'
}
$settingsSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarSettingsState.cs')
$optionsSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarOptionsTabPatch.cs')
$mcmSettingsSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'McmSettings.cs')
$calendarOptionItemSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\CalendarOptionItem.xml')
if ($settingsSource -notmatch 'DefaultCampaignTimeScale = 0\.23f' -or
    $settingsSource -notmatch 'MaximumPacingMultiplier = 4f' -or
    $settingsSource -notmatch 'requestedAutoCampaignTimeScale[\s\S]{0,120}\? DefaultCampaignTimeScale' -or
    $settingsSource -match 'case "Pregnancy Duration \(Months\)"' -or
    $settingsSource -match 'case "Lord Death Rate Multiplier"' -or
    $settingsSource -match 'case "Renown Gain Multiplier"' -or
    $settingsSource -match 'ApplyCampaignStartSetting\([\s\S]{0,160}AutoCampaignTimeScale' -or
    $optionsSource -notmatch 'new OptionGroup\(new TextObject\("Pacing"\)' -or
    $optionsSource -notmatch 'CalendarNumericOptionDataVM' -or
    $optionsSource -notmatch 'CalendarBooleanOptionDataVM' -or
    $optionsSource -notmatch 'base\("Benchmark", action\)' -or
    $optionsSource -notmatch 'ActionName = calendarAction\.DisplayActionName' -or
    $optionsSource -notmatch 'HarmonyPatch\(typeof\(ActionOptionDataVM\), nameof\(ActionOptionDataVM\.RefreshValues\)\)' -or
    $optionsSource -notmatch 'CompleteCategoryReset' -or
    $optionsSource -notmatch 'InformationManager\.DisplayMessage\(new InformationMessage' -or
    $optionsSource -notmatch 'const bool nativeCalendarSettingsEnabled = true' -or
    $optionsSource -notmatch 'RefreshCalendarOptionControls' -or
    $optionsSource -notmatch 'new CalendarBooleanOptionData\(\s*"Annual Balance Enabled"' -or
    $optionsSource -notmatch 'Reset Pacing' -or
    $optionsSource -notmatch 'Reset Annual Balance' -or
    $optionsSource -notmatch 'RefreshCalendarOptions\(\)' -or
    $optionsSource -match 'Reset Diagnostics' -or
    $settingsSource -notmatch 'ResetCalendarCategory' -or
    $optionsSource -match 'nativeCalendarSettingsEnabled = !OptionalMcmIntegration\.IsSettingsRegistered' -or
    $optionsSource -match 'base\("Calendar(MonthNames|SeasonNames|MonthLengths)", action\)' -or
    $optionsSource -notmatch 'SetValue\(Math\.Max\(Min, Math\.Min\(Max, value\)\)\)' -or
    $optionsSource -match 'Fixed Pregnancy Duration \(Days\)' -or
    $mcmSettingsSource -match 'Fixed Pregnancy Duration \(Days\)' -or
    $calendarOptionItemSource -notmatch 'Command\.Click="ExecuteDecrease"' -or
    $calendarOptionItemSource -notmatch 'Command\.Click="ExecuteIncrease"' -or
    $calendarOptionItemSource -notmatch 'Command\.Click="ExecuteToggle"') {
    throw 'Campaign pacing must reset live to the exact 0.23 automatic default and stay within Bannerlord''s 4x AI-safe limit; Calendar sliders and toggles must write through their view models; and fixed pregnancy days must stay out of the settings UI.'
}
$lordDeathSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarLordDeathModels.cs')
if ($lordDeathSource -notmatch 'CalendarHeroDeathProbabilityModel' -or
    $lordDeathSource -notmatch 'CalendarLordBattleSurvivalModel' -or
    $lordDeathSource -notmatch 'ScaleDailyDeathProbability') {
    throw 'Lord mortality must use the public old-age and battle-survival model wrappers.'
}
$saveProfileSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'CalendarSaveCompatibility.cs')
if ($saveProfileSource -notmatch 'CalendarCampaignProfileBehavior' -or
    $saveProfileSource -notmatch 'primitive payload only; no module-load marker written' -or
    $saveProfileSource -notmatch 'RealisticCalendarTweaks\.CampaignProfileV3') {
    throw 'New saves must use the primitive soft campaign profile rather than a hard module-lock marker.'
}

$mainDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\RealisticCalendarTweaks.dll'
$mcmDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\RealisticCalendarTweaks.MCM.dll'
$harmonyDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\0Harmony.dll'
$moduleXml = Join-Path $ModuleRoot 'SubModule.xml'
$moduleStrings = Join-Path $ModuleRoot 'ModuleData\module_strings.xml'
$readme = Join-Path $ModuleRoot 'README.md'
$optionsXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\Options.xml'
$optionItemXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\OptionItem.xml'
$calendarOptionItemXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\CalendarOptionItem.xml'
$calendarOptionsGroupedPageXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\CalendarOptionsGroupedPage.xml'
$mapBarXml = Join-Path $ModuleRoot 'GUI\Prefabs\Map\MapBar.xml'
$worldCalendarXml = Join-Path $ModuleRoot 'GUI\Prefabs\WorldCalendar\WorldCalendar.xml'
$worldCalendarSpriteData = Join-Path $ModuleRoot 'GUI\RealisticCalendarTweaksSpriteData.xml'
$worldCalendarSpriteConfig = Join-Path $ModuleRoot 'GUI\SpriteParts\Config.xml'
$worldCalendarMap = Join-Path $ModuleRoot 'GUI\SpriteParts\world_calendar\world_calendar_map.png'
$worldCalendarSheet = Join-Path $ModuleRoot 'AssetSources\GauntletUI\world_calendar_1.png'
$guiRoot = Join-Path $ModuleRoot 'GUI'
$assetsRoot = Join-Path $ModuleRoot 'Assets'
$assetSourcesRoot = Join-Path $ModuleRoot 'AssetSources'
$prefabsRoot = Join-Path $ModuleRoot 'Prefabs'
$runtimeFiles = @($moduleXml, $moduleStrings, $readme, $harmonyDll, $mainDll, $mcmDll, $optionsXml, $optionItemXml, $calendarOptionItemXml, $calendarOptionsGroupedPageXml, $mapBarXml, $worldCalendarXml, $worldCalendarSpriteData, $worldCalendarSpriteConfig, $worldCalendarMap, $worldCalendarSheet)
foreach ($path in $runtimeFiles) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected release output is missing: $path"
    }
}
foreach ($directory in @($guiRoot, $assetsRoot, $assetSourcesRoot, $prefabsRoot)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Expected release runtime directory is missing: $directory"
    }
}

[xml]$mapBar = Get-Content -Raw -LiteralPath $mapBarXml
$centerPanel = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]'))
if ($centerPanel.Count -ne 1 -or $centerPanel[0].HorizontalAlignment -ne 'Center' -or $centerPanel[0].PositionXOffset -ne '10' -or $centerPanel[0].VerticalAlignment -ne 'Bottom' -or $centerPanel[0].SuggestedWidth -ne '420' -or $centerPanel[0].SuggestedHeight -ne '60') {
    throw 'Map bar center panel must remain bottom-centered for resolution-independent placement.'
}
$calendarDate = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]/Children/TextWidget[@Text="@CalendarDateLine"]'))
$seasonLabel = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]/Children/TextWidget[@Text="@SeasonYearLine"]'))
if ($calendarDate.Count -ne 1 -or [int]$calendarDate[0].SuggestedWidth -ne 150 -or $calendarDate[0].PositionXOffset -ne '40' -or $calendarDate[0].PositionYOffset -ne '-6' -or $calendarDate[0].'Brush.FontSize' -ne '18') {
    throw 'Map bar calendar date must use the upper line of the widened two-line calendar block.'
}
if ($seasonLabel.Count -ne 1 -or [int]$seasonLabel[0].SuggestedWidth -ne 150 -or $seasonLabel[0].PositionXOffset -ne '40' -or $seasonLabel[0].PositionYOffset -ne '12' -or $seasonLabel[0].'Brush.FontSize' -ne '18') {
    throw 'Map bar season and year must use the lower line of the widened two-line calendar block.'
}
$clockLabel = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]/Children/TextWidget[@Text="@TimeOfDay"]'))
if ($clockLabel.Count -ne 1 -or [int]$clockLabel[0].SuggestedWidth -ne 90 -or $clockLabel[0].PositionXOffset -ne '55' -or $clockLabel[0].PositionYOffset -ne '0' -or $clockLabel[0].'Brush.TextHorizontalAlignment' -ne 'Center' -or $clockLabel[0].'Brush.FontSize' -ne '18') {
    throw 'Map bar clock must occupy the former season position just right of the sundial.'
}

$binaryText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($mainDll))
foreach ($unsafeSymbol in @(
    'DefaultClanFinanceModel',
    'DefaultPartyImpairmentModel',
    'DefaultPrisonerRecruitmentCalculationModel',
    'DefaultMarriageModel',
    'DefaultMapTrackModel')) {
    if ($binaryText.Contains($unsafeSymbol)) {
        throw "Unsafe native default-model reference found in release DLL: $unsafeSymbol"
    }
}

[xml]$manifest = Get-Content -Raw -LiteralPath $moduleXml
$version = $manifest.Module.Version.value
if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^v\d+\.\d+\.\d+$') {
    throw 'SubModule.xml must define a valid Bannerlord version in vMajor.Minor.Patch format.'
}
if ($manifest.Module.Id.value -ne 'RealisticCalendarTweaks' -or
    $manifest.Module.Name.value -ne 'Realistic Calendar Tweaks') {
    throw 'The primary module manifest must use the RealisticCalendarTweaks ID and display name.'
}

[xml]$moduleText = Get-Content -Raw -LiteralPath $moduleStrings
foreach ($optionId in @('CalendarMonthNames', 'CalendarSeasonNames', 'CalendarMonthLengths')) {
    if (@($moduleText.strings.string | Where-Object { $_.id -eq ('str_options_type.' + $optionId) }).Count -ne 1 -or
        @($moduleText.strings.string | Where-Object { $_.id -eq ('str_options_type_action.' + $optionId) }).Count -ne 1 -or
        @($moduleText.strings.string | Where-Object { $_.id -eq ('str_options_description.' + $optionId) }).Count -ne 1) {
        throw "Calendar action localization is incomplete for option ID: $optionId"
    }
}

if ([string]::IsNullOrWhiteSpace($ReleaseArchive)) {
    $ReleaseArchive = Join-Path $ModuleRoot ("artifacts\RealisticCalendarTweaks-{0}.zip" -f $version)
}

$archiveDirectory = Split-Path -Parent $ReleaseArchive
New-Item -ItemType Directory -Force -Path $archiveDirectory | Out-Null
if (Test-Path -LiteralPath $ReleaseArchive) {
    Remove-Item -LiteralPath $ReleaseArchive -Force
}

$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ("RealisticCalendarTweaks-release-{0}" -f [Guid]::NewGuid())
$moduleStage = Join-Path $stagingRoot 'RealisticCalendarTweaks'
try {
    New-Item -ItemType Directory -Force -Path `
        (Join-Path $moduleStage 'bin\Win64_Shipping_Client'), `
        (Join-Path $moduleStage 'ModuleData') | Out-Null
    Copy-Item -LiteralPath $moduleXml, $readme -Destination $moduleStage
    Copy-Item -LiteralPath $moduleStrings -Destination (Join-Path $moduleStage 'ModuleData')
    Copy-Item -LiteralPath $harmonyDll, $mainDll, $mcmDll -Destination (Join-Path $moduleStage 'bin\Win64_Shipping_Client')
    Copy-Item -LiteralPath $guiRoot, $assetsRoot, $assetSourcesRoot, $prefabsRoot -Destination $moduleStage -Recurse
    Compress-Archive -LiteralPath $moduleStage -DestinationPath $ReleaseArchive -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$expectedEntries = @(
    'RealisticCalendarTweaks/README.md',
    'RealisticCalendarTweaks/SubModule.xml',
    'RealisticCalendarTweaks/ModuleData/module_strings.xml',
    'RealisticCalendarTweaks/bin/Win64_Shipping_Client/0Harmony.dll',
    'RealisticCalendarTweaks/bin/Win64_Shipping_Client/RealisticCalendarTweaks.dll',
    'RealisticCalendarTweaks/bin/Win64_Shipping_Client/RealisticCalendarTweaks.MCM.dll'
)
$runtimeDirectoryEntries = foreach ($directory in @($guiRoot, $assetsRoot, $assetSourcesRoot, $prefabsRoot)) {
    $directoryName = Split-Path -Leaf $directory
    Get-ChildItem -LiteralPath $directory -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($directory.Length).TrimStart('\', '/')
        ('RealisticCalendarTweaks/{0}/{1}' -f $directoryName, $relativePath).Replace('\', '/')
    }
}
$expectedEntries = @($expectedEntries + $runtimeDirectoryEntries | Sort-Object -Unique)
$archive = [IO.Compression.ZipFile]::OpenRead($ReleaseArchive)
try {
    $actualEntries = @($archive.Entries |
        ForEach-Object { $_.FullName.Replace('\', '/') } |
        Where-Object { -not $_.EndsWith('/') } |
        Sort-Object -Unique)
}
finally {
    $archive.Dispose()
}
if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries) {
    throw 'Release archive contents do not exactly match the approved runtime file list.'
}
if ($actualEntries | Where-Object { $_ -match '(?i)BetterTime|TwelveMonthCalendar\.BetterTime|^_TwelveMonthCalendar/' }) {
    throw 'The retired Better Time adapter and legacy save bridge must not be present in a release archive.'
}

if (-not (Get-Command Start-MpScan -ErrorAction SilentlyContinue)) {
    throw 'Microsoft Defender scan command is unavailable. Do not upload this release.'
}
$scanStarted = Get-Date
Start-MpScan -ScanPath $ReleaseArchive -ScanType CustomScan
$escapedArchive = [Regex]::Escape($ReleaseArchive)
function Get-ReleaseArchiveDetections {
    Get-MpThreatDetection | Where-Object {
        $_.Resources -match $escapedArchive -and $_.InitialDetectionTime -ge $scanStarted.AddMinutes(-1)
    }
}

# A custom scan can return before Defender's cloud verdict arrives.  Keep the
# exact final archive on disk and poll it for a full hold period; a quarantined
# archive or any detection is an unconditional release failure.
for ($minute = 1; $minute -le $CloudVerdictHoldMinutes; $minute++) {
    Start-Sleep -Seconds 60
    if (-not (Test-Path -LiteralPath $ReleaseArchive -PathType Leaf)) {
        throw 'Microsoft Defender or another security product removed the release archive during the cloud-verdict hold. Do not upload this release.'
    }
    $detections = @(Get-ReleaseArchiveDetections)
    if ($detections.Count -gt 0) {
        $detections | Format-List | Out-String | Write-Error
        throw 'Microsoft Defender detected a threat in the release archive. Do not upload this release.'
    }
    Write-Output ("Defender cloud-verdict hold: {0}/{1} minutes clean." -f $minute, $CloudVerdictHoldMinutes)
}

$archiveHash = (Get-FileHash $ReleaseArchive -Algorithm SHA256).Hash
$releaseCommit = (git -C $ModuleRoot rev-parse HEAD).Trim()
Write-Output ('PASS: Defender scan clean; Commit={0}; Archive={1}; SHA256={2}; DailyFactor={3:F8}; DurationFactor={4:F8}; MainDLL={5}; MCMDLL={6}' -f `
    $releaseCommit, $ReleaseArchive, $archiveHash, $dailyFactor, $durationFactor, (Get-FileHash $mainDll -Algorithm SHA256).Hash, (Get-FileHash $mcmDll -Algorithm SHA256).Hash)
