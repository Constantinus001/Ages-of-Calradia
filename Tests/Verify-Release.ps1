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

$mainDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\TwelveMonthCalendar.dll'
$mcmDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\TwelveMonthCalendar.MCM.dll'
$harmonyDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\0Harmony.dll'
$moduleXml = Join-Path $ModuleRoot 'SubModule.xml'
$readme = Join-Path $ModuleRoot 'README.md'
$optionsXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\Options.xml'
$mapBarXml = Join-Path $ModuleRoot 'GUI\Prefabs\Map\MapBar.xml'
$runtimeFiles = @($moduleXml, $readme, $harmonyDll, $mainDll, $mcmDll, $optionsXml, $mapBarXml)
foreach ($path in $runtimeFiles) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected release output is missing: $path"
    }
}

[xml]$mapBar = Get-Content -Raw -LiteralPath $mapBarXml
$centerPanel = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]'))
if ($centerPanel.Count -ne 1 -or $centerPanel[0].HorizontalAlignment -ne 'Center' -or $centerPanel[0].VerticalAlignment -ne 'Bottom') {
    throw 'Map bar center panel must remain bottom-centered for resolution-independent placement.'
}
$calendarDate = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]/Children/TextWidget[@Text="@Date"]'))
if ($calendarDate.Count -ne 1) {
    throw 'Map bar must contain exactly one calendar date label.'
}
if ($calendarDate[0].PositionYOffset -ne '-2' -or $calendarDate[0].'Brush.FontSize' -ne '17') {
    throw 'Map bar calendar date must retain Y=-2 and 17-point text.'
}
if ([int]$calendarDate[0].SuggestedWidth -ne 230 -or $calendarDate[0].PositionXOffset -ne '-50') {
    throw 'Map bar calendar date must move 20 pixels left while preserving sundial clearance.'
}
$seasonLabel = @($mapBar.SelectNodes('//MapCurrentTimeVisualWidget[@Id="CenterPanel"]/Children/TextWidget[@Text="@Season"]'))
if ($seasonLabel.Count -ne 1 -or $seasonLabel[0].PositionXOffset -ne '95' -or $seasonLabel[0].VerticalAlignment -ne 'Bottom' -or $seasonLabel[0].PositionYOffset -ne '-4') {
    throw 'Map bar season label must be independently placed two pixels higher at the marked position.'
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

$subModuleSource = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'MySubModule.cs')
if ($subModuleSource -match 'TwelveMonthCalendar\.BetterTime\.dll' -or
    $subModuleSource -match 'Assembly\.LoadFrom') {
    throw 'The public release must not contain the optional dynamically loaded Better Time adapter.'
}

[xml]$manifest = Get-Content -Raw -LiteralPath $moduleXml
$version = $manifest.Module.Version.value
if ([string]::IsNullOrWhiteSpace($version) -or $version -notmatch '^v\d+\.\d+\.\d+$') {
    throw 'SubModule.xml must define a valid Bannerlord version in vMajor.Minor.Patch format.'
}

if ([string]::IsNullOrWhiteSpace($ReleaseArchive)) {
    $ReleaseArchive = Join-Path $ModuleRoot ("artifacts\TwelveMonthCalendar-{0}.zip" -f $version)
}

$archiveDirectory = Split-Path -Parent $ReleaseArchive
New-Item -ItemType Directory -Force -Path $archiveDirectory | Out-Null
if (Test-Path -LiteralPath $ReleaseArchive) {
    Remove-Item -LiteralPath $ReleaseArchive -Force
}

$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) ("TwelveMonthCalendar-release-{0}" -f [Guid]::NewGuid())
$moduleStage = Join-Path $stagingRoot '_TwelveMonthCalendar'
try {
    New-Item -ItemType Directory -Force -Path `
        (Join-Path $moduleStage 'bin\Win64_Shipping_Client'), `
        (Join-Path $moduleStage 'GUI\Prefabs\Options\SPOptions'), `
        (Join-Path $moduleStage 'GUI\Prefabs\Map') | Out-Null
    Copy-Item -LiteralPath $moduleXml, $readme -Destination $moduleStage
    Copy-Item -LiteralPath $harmonyDll, $mainDll, $mcmDll -Destination (Join-Path $moduleStage 'bin\Win64_Shipping_Client')
    Copy-Item -LiteralPath $optionsXml -Destination (Join-Path $moduleStage 'GUI\Prefabs\Options\SPOptions')
    Copy-Item -LiteralPath $mapBarXml -Destination (Join-Path $moduleStage 'GUI\Prefabs\Map')
    Compress-Archive -LiteralPath $moduleStage -DestinationPath $ReleaseArchive -CompressionLevel Optimal
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$expectedEntries = @(
    '_TwelveMonthCalendar/README.md',
    '_TwelveMonthCalendar/SubModule.xml',
    '_TwelveMonthCalendar/bin/Win64_Shipping_Client/0Harmony.dll',
    '_TwelveMonthCalendar/bin/Win64_Shipping_Client/TwelveMonthCalendar.dll',
    '_TwelveMonthCalendar/bin/Win64_Shipping_Client/TwelveMonthCalendar.MCM.dll',
    '_TwelveMonthCalendar/GUI/Prefabs/Options/SPOptions/Options.xml',
    '_TwelveMonthCalendar/GUI/Prefabs/Map/MapBar.xml'
)
$archive = [IO.Compression.ZipFile]::OpenRead($ReleaseArchive)
try {
    $actualEntries = @($archive.Entries |
        ForEach-Object { $_.FullName.Replace('\', '/') } |
        Where-Object { -not $_.EndsWith('/') })
}
finally {
    $archive.Dispose()
}
if (Compare-Object -ReferenceObject $expectedEntries -DifferenceObject $actualEntries) {
    throw 'Release archive contents do not exactly match the approved runtime file list.'
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
