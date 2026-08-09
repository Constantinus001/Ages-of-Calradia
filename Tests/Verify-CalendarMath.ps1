param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BannerlordDir = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$CalendarAssemblyPath,
    [switch]$ExpectRefugeSystemEnabled
)

$ErrorActionPreference = 'Stop'

$gameBin = Join-Path $BannerlordDir 'bin\Win64_Shipping_Client'
foreach ($assemblyName in @(
    'TaleWorlds.Library.dll',
    'TaleWorlds.Core.dll',
    'TaleWorlds.Localization.dll',
    'TaleWorlds.ObjectSystem.dll',
    'TaleWorlds.SaveSystem.dll',
    'TaleWorlds.CampaignSystem.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $gameBin $assemblyName)) | Out-Null
}

if ([string]::IsNullOrWhiteSpace($CalendarAssemblyPath)) {
    $CalendarAssemblyPath = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\RealisticCalendarTweaks.dll'
}
$calendarAssembly = [Reflection.Assembly]::LoadFrom($CalendarAssemblyPath)
$calendarMath = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarTimeMath', $true)

function Invoke-CalendarMath([string]$Name, [object[]]$Arguments) {
    $method = $calendarMath.GetMethod(
        $Name,
        [Reflection.BindingFlags]'Static,NonPublic')
    if ($null -eq $method) {
        throw "CalendarTimeMath method was not found: $Name"
    }

    return $method.Invoke($null, $Arguments)
}

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) {
        throw "$Name failed. Expected '$Expected'; actual '$Actual'."
    }
}

function Assert-True($Actual, [string]$Name) {
    if (-not $Actual) {
        throw "$Name failed. Expected true; actual '$Actual'."
    }
}

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Name) {
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance) {
        throw "$Name failed. Expected '$Expected' +/- '$Tolerance'; actual '$Actual'."
    }
}

try {
    $calendarTypes = @($calendarAssembly.GetTypes())
}
catch [Reflection.ReflectionTypeLoadException] {
    $calendarTypes = @($_.Exception.Types | Where-Object { $null -ne $_ })
}

$customSaveDefiners = @($calendarTypes | Where-Object {
    $null -ne $_.BaseType -and
    $_.BaseType.FullName -eq 'TaleWorlds.SaveSystem.SaveableTypeDefiner'
})
Assert-Equal 0 $customSaveDefiners.Count 'Removable-save custom type definer count'

$moduleOwnedSaveFields = @($calendarTypes | ForEach-Object {
    $_.GetFields([Reflection.BindingFlags]'Public,NonPublic,Instance,Static') | Where-Object {
        @($_.GetCustomAttributesData() | Where-Object {
            $_.AttributeType.FullName -eq 'TaleWorlds.SaveSystem.SaveableFieldAttribute'
        }).Count -gt 0
    }
})
Assert-Equal 0 $moduleOwnedSaveFields.Count 'Removable-save custom saveable field count'
$featureSettingsType = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarSettingsState', $true)
$refugeFeatureProperty = $featureSettingsType.GetProperty(
    'RefugeSystemEnabled',
    [Reflection.BindingFlags]'Static,NonPublic')
Assert-Equal ([bool]$ExpectRefugeSystemEnabled) ([bool]$refugeFeatureProperty.GetValue($null)) 'Build-specific refuge feature flag'

Assert-Equal $true (Invoke-CalendarMath 'IsLeapYear' @(1084)) 'Leap year 1084'
Assert-Equal $false (Invoke-CalendarMath 'IsLeapYear' @(1100)) 'Century year 1100'
Assert-Equal $true (Invoke-CalendarMath 'IsLeapYear' @(1200)) 'Four-hundred-year 1200'
Assert-Equal 366 (Invoke-CalendarMath 'GetYearLength' @(1084)) 'Leap-year length'
Assert-Equal 365 (Invoke-CalendarMath 'GetYearLength' @(1085)) 'Common-year length'
Assert-Equal 80 (Invoke-CalendarMath 'GetSeasonStartDayOfYear' @(1084, 0)) 'Leap-year spring boundary'
Assert-Equal 79 (Invoke-CalendarMath 'GetSeasonStartDayOfYear' @(1085, 0)) 'Common-year spring boundary'

$campaignAssembly = [AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GetName().Name -eq 'TaleWorlds.CampaignSystem' } |
    Select-Object -First 1
$fastForwardModeMethod = $calendarMath.GetMethod(
    'IsFastForwardMode',
    [Reflection.BindingFlags]'Static,NonPublic')
$timeControlMode = $campaignAssembly.GetType('TaleWorlds.CampaignSystem.CampaignTimeControlMode', $true)
Assert-Equal $false ($fastForwardModeMethod.Invoke($null, @([Enum]::Parse($timeControlMode, 'StoppablePlay')))) 'Normal mode detection'
Assert-Equal $true ($fastForwardModeMethod.Invoke($null, @([Enum]::Parse($timeControlMode, 'StoppableFastForward')))) 'Fast-forward mode detection'

$campaignTimeType = $campaignAssembly.GetType('TaleWorlds.CampaignSystem.CampaignTime', $true)
$campaignTimeType.GetField(
    'TimeTicksPerDay',
    [Reflection.BindingFlags]'Static,NonPublic').SetValue($null, [long]1000000)
$campaignDaysFactory = $campaignTimeType.GetMethod(
    'Days',
    [Reflection.BindingFlags]'Static,Public',
    $null,
    [Type[]]@([single]),
    $null)
$ageAtMethod = $calendarMath.GetMethod(
    'GetLegacyCompatibleHeroAgeAt',
    [Reflection.BindingFlags]'Static,NonPublic')
$markLegacyAge = $featureSettingsType.GetMethod(
    'MarkLegacySaveAgeCompatibility',
    [Reflection.BindingFlags]'Static,NonPublic')
$beginCampaignSession = $featureSettingsType.GetMethod(
    'BeginCampaignSession',
    [Reflection.BindingFlags]'Static,NonPublic')
$looksLikeNativeBasis = $calendarMath.GetMethod(
    'LooksLikeNativeTimeBasis',
    [Reflection.BindingFlags]'Static,NonPublic')
$toCalendarAbsoluteDays = $calendarMath.GetMethod(
    'ToCalendarAbsoluteDays',
    [Reflection.BindingFlags]'Static,NonPublic',
    $null,
    [Type[]]@($campaignTimeType),
    $null)
$durationToYears = $calendarMath.GetMethod(
    'DurationToYears',
    [Reflection.BindingFlags]'Static,NonPublic')
$durationToSeasons = $calendarMath.GetMethod(
    'DurationToSeasons',
    [Reflection.BindingFlags]'Static,NonPublic')
$nativeCampaignStartDay = [double]$calendarMath.GetProperty(
    'NativeCampaignStartDay',
    [Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
$gregorianCampaignStartDay = [double]$calendarMath.GetProperty(
    'GregorianCampaignStartDay',
    [Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
$nativeCampaignStart = $campaignDaysFactory.Invoke($null, @([single]$nativeCampaignStartDay))
$gregorianCampaignStart = $campaignDaysFactory.Invoke($null, @([single]$gregorianCampaignStartDay))
Assert-True ($looksLikeNativeBasis.Invoke($null, @($nativeCampaignStart))) 'Native raw campaign-time basis detection'
Assert-Equal $false ($looksLikeNativeBasis.Invoke($null, @($gregorianCampaignStart))) 'Gregorian raw campaign-time basis detection'
$markLegacyAge.Invoke($null, @([double]100000.0)) | Out-Null
$oneCalendarYearDuration = $campaignDaysFactory.Invoke($null, @([single]365.2425))
$oneCalendarSeasonDuration = $campaignDaysFactory.Invoke($null, @([single](365.2425 / 4.0)))
Assert-Near 1.0 ([double]$durationToYears.Invoke($null, @($oneCalendarYearDuration))) 0.0001 'Duration years exclude absolute-date epoch offset'
Assert-Near 1.0 ([double]$durationToSeasons.Invoke($null, @($oneCalendarSeasonDuration))) 0.0001 'Duration seasons exclude absolute-date epoch offset'
$nativeThirtyYearBirth = $campaignDaysFactory.Invoke($null, @([single]97480.0))
$nativeFortyYearBirth = $campaignDaysFactory.Invoke($null, @([single]96640.0))
$cutoverTime = $campaignDaysFactory.Invoke($null, @([single]100000.0))
$oneGregorianYearLater = $campaignDaysFactory.Invoke($null, @([single]100365.2425))
$postCutoverBirth = $campaignDaysFactory.Invoke($null, @([single]100100.0))
$postCutoverReference = $campaignDaysFactory.Invoke($null, @([single]100465.2425))
Assert-Near 30.0 ([double]$ageAtMethod.Invoke($null, @($nativeThirtyYearBirth, $cutoverTime))) 0.0001 'Legacy age preserved at cutover'
Assert-Near 31.0 ([double]$ageAtMethod.Invoke($null, @($nativeThirtyYearBirth, $oneGregorianYearLater))) 0.0001 'Legacy hero future Gregorian aging'
Assert-Near 1.0 ([double]$ageAtMethod.Invoke($null, @($postCutoverBirth, $postCutoverReference))) 0.0001 'Post-cutover newborn Gregorian aging'
Assert-Near 40.0 ([double]$ageAtMethod.Invoke($null, @($nativeFortyYearBirth, $cutoverTime))) 0.0001 'Legacy dead-hero age at death'
Assert-Near $gregorianCampaignStartDay ([double]$toCalendarAbsoluteDays.Invoke($null, @($nativeCampaignStart))) 0.01 'Native campaign epoch maps to Gregorian April 1084'
Assert-Equal 1084 (Invoke-CalendarMath 'GetYear' @($nativeCampaignStart)) 'Mapped native-save calendar year'
Assert-Equal 3 (Invoke-CalendarMath 'GetMonth' @($nativeCampaignStart)) 'Mapped native-save calendar month'
$beginCampaignSession.Invoke($null, @()) | Out-Null
Assert-Equal $false ([bool]$featureSettingsType.GetProperty('IsLegacySaveAgeCompatibility', [Reflection.BindingFlags]'Static,NonPublic').GetValue($null)) 'Cross-campaign legacy age reset'

$profileType = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarCampaignProfile', $true)
$captureProfile = $profileType.GetMethod('Capture', [Reflection.BindingFlags]'Static,Public')
$profile = $captureProfile.Invoke($null, @())
Assert-Equal 5 $profile.SchemaVersion 'Campaign profile schema'
Assert-Equal 1.0 $profile.NormalPlayTimeMultiplier 'Campaign profile normal pace'
Assert-Equal 4.0 $profile.FastForwardTimeMultiplier 'Campaign profile fast-forward speed'
Assert-True (-not [string]::IsNullOrWhiteSpace($profile.Fingerprint)) 'Campaign profile fingerprint'
$profileValidationArguments = [object[]]@($null)
Assert-True ($profileType.GetMethod('TryValidate').Invoke($profile, $profileValidationArguments)) 'Campaign profile validation'

$settingsType = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarSettingsState', $true)
$monthNameArguments = [object[]]@(
    'January|February|March|April|May|June|July|August|September|October|November|December',
    $null,
    $null)
Assert-True ($settingsType.GetMethod('TryParseMonthNamesDelimited').Invoke($null, $monthNameArguments)) 'Month-name editor parser'
Assert-Equal 12 @($monthNameArguments[1]).Length 'Month-name editor count'
$seasonNameArguments = [object[]]@('Spring|Summer|Autumn|Winter', $null, $null)
Assert-True ($settingsType.GetMethod('TryParseSeasonNamesDelimited').Invoke($null, $seasonNameArguments)) 'Season-name editor parser'
Assert-Equal 4 @($seasonNameArguments[1]).Length 'Season-name editor count'
$monthLengthArguments = [object[]]@('31|28|31|30|31|30|31|31|30|31|30|31', $null, $null)
Assert-True ($settingsType.GetMethod('TryParseMonthLengthsDelimited').Invoke($null, $monthLengthArguments)) 'Month-length editor parser'
Assert-Equal 365 ((@($monthLengthArguments[1]) | Measure-Object -Sum).Sum) 'Month-length editor total'

$legacyProfile = $captureProfile.Invoke($null, @())
$legacyProfile.SchemaVersion = 2
$legacyProfile.NormalPlayTimeMultiplier = 1.25
$legacyProfile.FastForwardTimeMultiplier = 2.5
Assert-True ($legacyProfile.TryUpgradeLegacyProfile()) 'Legacy profile upgrade'
Assert-Equal 5 $legacyProfile.SchemaVersion 'Legacy profile schema migration'
Assert-Equal 1.0 $legacyProfile.NormalPlayTimeMultiplier 'Legacy profile fixed normal pace migration'
Assert-Equal 4.0 $legacyProfile.FastForwardTimeMultiplier 'Legacy profile fast-forward speed migration clamps to AI-safe maximum'
Assert-Equal $false $legacyProfile.LegacyNativeAgeBasis 'Legacy profile defers native-basis detection to saved raw time'

$v15Profile = $captureProfile.Invoke($null, @())
$v15Profile.SchemaVersion = 3
$v15Profile.FastForwardTimeMultiplier = 128.0
Assert-True ($v15Profile.TryUpgradeLegacyProfile()) 'v1.5 profile upgrade'
Assert-Equal 5 $v15Profile.SchemaVersion 'v1.5 profile schema migration'
Assert-Equal 4.0 $v15Profile.FastForwardTimeMultiplier 'v1.5 profile fast-forward clamp'
Assert-True $v15Profile.AnnualBalanceEnabled 'v1.5 profile annual-balance master migration'

$profile.NormalPlayTimeMultiplier = 1.0
$profile.FastForwardTimeMultiplier = 4.0
$profile.RefreshFingerprint()
$settingsType.GetMethod('ApplyPersistedCampaignProfile', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @($profile)) | Out-Null
Assert-Equal 1.0 ($settingsType.GetProperty('NormalPlayTimeMultiplier').GetValue($null)) 'Saved profile fixed normal pace restore'
Assert-Equal 4.0 ($settingsType.GetProperty('FastForwardTimeMultiplier').GetValue($null)) 'Saved profile fast-forward speed restore clamps to AI-safe maximum'

$serializedProfile = $profile.Serialize()
$deserializeArguments = [object[]]@($serializedProfile, $null, $null)
Assert-True ($profileType.GetMethod('TryDeserialize', [Reflection.BindingFlags]'Static,Public').Invoke($null, $deserializeArguments)) 'Soft profile serialization round trip'
$roundTripProfile = $deserializeArguments[1]
Assert-Equal 5 $roundTripProfile.SchemaVersion 'Soft profile round-trip schema'
Assert-Equal 4.0 $roundTripProfile.FastForwardTimeMultiplier 'Soft profile round-trip fast-forward speed'
Assert-Equal $profile.AnnualBalanceEnabled $roundTripProfile.AnnualBalanceEnabled 'Soft profile round-trip annual-balance master'

$lordDeathBalance = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarLordDeathBalance', $true)
$scaleDailyDeath = $lordDeathBalance.GetMethod('ScaleDailyDeathProbability', [Reflection.BindingFlags]'Static,NonPublic')
$scaleBattleSurvival = $lordDeathBalance.GetMethod('ScaleBattleSurvivalChance', [Reflection.BindingFlags]'Static,NonPublic')
$scaledDailyDeath = [single]$scaleDailyDeath.Invoke($null, @([single]0.02))
$scaledBattleSurvival = [single]$scaleBattleSurvival.Invoke($null, @([single]0.80))
Assert-True ($scaledDailyDeath -gt 0 -and $scaledDailyDeath -lt 0.02) 'Lord old-age mortality reduction'
Assert-True ([Math]::Abs($scaledBattleSurvival - 0.96) -lt 0.0001) 'Lord battle survival reduction'

$auditType = $calendarAssembly.GetType('TwelveMonthCalendar.CalendarPatchSafetyAudit', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic'
$auditType.GetMethod('BeginStartupAudit', $flags).Invoke($null, @()) | Out-Null
Assert-True ($auditType.GetMethod('ValidateCampaignTimeCalendarTargets', $flags).Invoke($null, @())) 'CampaignTime target audit'
Assert-True ($auditType.GetMethod('ValidateCampaignTimeStringTarget', $flags).Invoke($null, @())) 'CampaignTime string target audit'
$trackerType = $campaignAssembly.GetType('TaleWorlds.CampaignSystem.MapTimeTracker', $true)
$trackerTick = $trackerType.GetMethod('Tick', [Reflection.BindingFlags]'Instance,Public,NonPublic', $null, @([single]), $null)
$campaignType = $campaignAssembly.GetType('TaleWorlds.CampaignSystem.Campaign', $true)
$campaignTick = $campaignType.GetMethod('TickMapTime', [Reflection.BindingFlags]'Instance,Public,NonPublic', $null, @([single]), $null)
Assert-True ($auditType.GetMethod('ValidateMapTimeTrackerTarget', $flags).Invoke($null, @($trackerTick))) 'Map time target audit'
Assert-True ($auditType.GetMethod('ValidateCampaignPacingTarget', $flags).Invoke($null, @($campaignTick))) 'Campaign pacing target audit'
$auditType.GetMethod('EnsureCoreTargetsValidated', $flags).Invoke($null, @()) | Out-Null

Write-Output 'PASS: Calendar math, removable-save profile, pacing, and target-audit checks passed.'
