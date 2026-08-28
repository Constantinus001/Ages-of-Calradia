param(
    [string]$BannerlordDir = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ModuleRoot = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Ages Of Calradia',
    [string]$HarmonyPath = (Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\0Harmony.dll'),
    [string]$SidecarPath = (Join-Path $PSScriptRoot 'bin\Win64_Shipping_Client\AgesOfCalradia.Approved560CalendarFixes.dll')
)

$ErrorActionPreference = 'Stop'
$gameBin = Join-Path $BannerlordDir 'bin\Win64_Shipping_Client'
$moduleBin = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client'
[Reflection.Assembly]::LoadFrom($HarmonyPath) | Out-Null
foreach ($name in @(
    'TaleWorlds.Library.dll',
    'TaleWorlds.DotNet.dll',
    'TaleWorlds.Engine.dll',
    'TaleWorlds.InputSystem.dll',
    'TaleWorlds.GauntletUI.dll',
    'TaleWorlds.Engine.GauntletUI.dll',
    'TaleWorlds.ScreenSystem.dll',
    'TaleWorlds.MountAndBlade.GauntletUI.Widgets.dll',
    'TaleWorlds.Core.dll',
    'TaleWorlds.Localization.dll',
    'TaleWorlds.ObjectSystem.dll',
    'TaleWorlds.SaveSystem.dll',
    'TaleWorlds.CampaignSystem.dll',
    'TaleWorlds.MountAndBlade.dll',
    'TaleWorlds.Core.ViewModelCollection.dll',
    'TaleWorlds.CampaignSystem.ViewModelCollection.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $gameBin $name)) | Out-Null
}

$approvedMain = [Reflection.Assembly]::LoadFrom((Join-Path $moduleBin 'AgesOfCalradia.dll'))
$mainType = $approvedMain.GetType('AgesOfCalradia.MySubModule', $true)
$mainInstance = [Activator]::CreateInstance($mainType)
$mainLoad = $mainType.GetMethod('OnSubModuleLoad', [Reflection.BindingFlags]'Instance,NonPublic')
$mainLoad.Invoke($mainInstance, @()) | Out-Null
$sidecar = [Reflection.Assembly]::LoadFrom($SidecarPath)
$type = $sidecar.GetType(
    'AgesOfCalradia.Approved560CalendarFixes.Approved560CalendarFixesSubModule',
    $true)
$instance = [Activator]::CreateInstance($type)
$load = $type.GetMethod('OnSubModuleLoad', [Reflection.BindingFlags]'Instance,NonPublic')
$load.Invoke($instance, @()) | Out-Null

$owner = 'AgesOfCalradia.Approved560CalendarFixes.560F1B51'
$patched = @()
foreach ($original in [HarmonyLib.Harmony]::GetAllPatchedMethods()) {
    $info = [HarmonyLib.Harmony]::GetPatchInfo($original)
    $owned = @($info.Prefixes + $info.Postfixes + $info.Transpilers + $info.Finalizers |
        Where-Object { $_.owner -eq $owner })
    if ($owned.Count -gt 0) {
        $patched += [pscustomobject]@{
            Target = $original.DeclaringType.FullName + '::' + $original.Name
            PatchCount = $owned.Count
        }
    }
}

$patched | Sort-Object Target | Format-Table -AutoSize
$count = ($patched | Measure-Object -Property PatchCount -Sum).Sum
if ($count -lt 18) {
    throw "Expected at least 18 compatibility patches; found $count."
}

$sidecarWheelOverrides = @($patched | Where-Object {
    $_.Target -eq 'TwelveMonthCalendar.StrategicMapZoomScrollablePanel::OnPreviewMouseScroll' -or
    $_.Target -eq 'TwelveMonthCalendar.WorldCalendarScreen::OnTick'
})
if ($sidecarWheelOverrides.Count -ne 0) {
    throw 'The sidecar must not override the approved main DLL strategic wheel/drag implementation.'
}

$scrollWidget = $sidecar.GetType(
    'AgesOfCalradia.Approved560CalendarFixes.WorldEventsRowSnapScrollablePanel',
    $true)
if (-not $scrollWidget.IsSubclassOf([TaleWorlds.GauntletUI.BaseTypes.ScrollablePanel])) {
    throw 'The UI REDESIGN row-snap widget must derive from Gauntlet ScrollablePanel.'
}
$sidecarSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Approved560CalendarFixesSubModule.cs') -Raw
foreach ($requiredScrollContract in @(
    'public sealed class WorldEventsRowSnapScrollablePanel : ScrollablePanel',
    'protected override bool OnPreviewMouseScroll()',
    'SetVerticalScrollTarget(_wheelTarget, 0.10f)')) {
    if (-not $sidecarSource.Contains($requiredScrollContract)) {
        throw "World Events scrolling contract missing: $requiredScrollContract"
    }
}
if (@($patched | Where-Object {
    $_.Target -eq 'TwelveMonthCalendar.CalendarWorldLedgerVM::get_StrategicMapLegendHeight'
}).Count -ne 0) {
    throw 'Strategic legend geometry is prefab-owned and must not be altered by Harmony.'
}

$clockPatch = $sidecar.GetType(
    'AgesOfCalradia.Approved560CalendarFixes.MapClockMeridiemLayoutPatch',
    $true)
$formatClock = $clockPatch.GetMethod(
    'FormatForVerification',
    [Reflection.BindingFlags]'Static,NonPublic')
$morningClock = [string]$formatClock.Invoke($null, @('09:26', 9))
$eveningClock = [string]$formatClock.Invoke($null, @('9:26 PM', 21))
if ($morningClock -ne "09:26`nAM" -or $eveningClock -ne "9:26`nPM" -or
    @($patched | Where-Object {
        $_.Target -eq 'TwelveMonthCalendar.CalendarMapTimeControlVM::RefreshClock'
    }).Count -ne 1) {
    throw 'Campaign map clock must render AM/PM on a second line exactly once.'
}

$foodFix = $sidecar.GetType(
    'AgesOfCalradia.Approved560CalendarFixes.TownMarketFoodAccountingFix',
    $true)
$combine = $foodFix.GetMethod(
    'CombineForVerification',
    [Reflection.BindingFlags]'Static,NonPublic')
$directOnly = [single]$combine.Invoke($null, @([single]-10, [single]-2, $false, [single]0.23))
$withMarket = [single]$combine.Invoke($null, @([single]-10, [single]-2, $true, [single]0.23))
if ([Math]::Abs($directOnly - [single]-2.3) -gt 0.0001 -or
    [Math]::Abs($withMarket - [single]-0.46) -gt 0.0001) {
    throw "Town food accounting must calculate with vanilla values and scale only the selected final result: direct=$directOnly market=$withMarket"
}

$cadence = $sidecar.GetType(
    'AgesOfCalradia.Approved560CalendarFixes.VanillaFoodCadence',
    $true)
$scaleDemand = $cadence.GetMethod(
    'ScaleDemandForVerification',
    [Reflection.BindingFlags]'Static,NonPublic')
$foodDemand = [single]$scaleDemand.Invoke($null, @([single]100, $true, [single]0.23))
$nonFoodDemand = [single]$scaleDemand.Invoke($null, @([single]100, $false, [single]0.23))
if ([Math]::Abs($foodDemand - [single]100) -gt 0.0001 -or
    [Math]::Abs($nonFoodDemand - [single]23) -gt 0.0001) {
    throw "Food demand must remain vanilla while non-food demand is annualized once: food=$foodDemand nonFood=$nonFoodDemand"
}

if ($null -eq $approvedMain.GetType('TwelveMonthCalendar.LegacySaveHeroAgePatch', $false)) {
    throw 'The approved main DLL no longer contains the save-age compatibility patch.'
}
if (@($patched | Where-Object {
    $_.Target -match 'Political|Territory|Island|Lake|Texture|Mesh'
}).Count -gt 0) {
    throw 'The compatibility sidecar patched a renderer target.'
}

$retiredTypes = @(
    'TwelveMonthCalendar.MapTimeTrackerPatch',
    'TwelveMonthCalendar.WorkshopProductionBalancePatch',
    'TwelveMonthCalendar.WorkshopFoodContextPatch',
    'TwelveMonthCalendar.VillageFoodProductionBalancePatch',
    'TwelveMonthCalendar.VillageProductionBalancePatch',
    'TwelveMonthCalendar.SettlementDemandBalancePatch',
    'TwelveMonthCalendar.SettlementBudgetBalancePatch',
    'TwelveMonthCalendar.SettlementMarketSmoothingBalancePatch',
    'TwelveMonthCalendar.KingdomWarCooldownPatch')
foreach ($original in [HarmonyLib.Harmony]::GetAllPatchedMethods()) {
    $info = [HarmonyLib.Harmony]::GetPatchInfo($original)
    foreach ($patch in @($info.Prefixes + $info.Postfixes + $info.Transpilers + $info.Finalizers)) {
        if ($null -ne $patch.PatchMethod -and
            $null -ne $patch.PatchMethod.DeclaringType -and
            $retiredTypes -contains $patch.PatchMethod.DeclaringType.FullName) {
            throw "Superseded approved-DLL patch remains active: $($patch.PatchMethod.DeclaringType.FullName)."
        }
    }
}

Write-Output "PASS: approved main + v1.5.14 production sidecar registered $count fixes, kept food demand native, scaled only final town food balance, preserved save-age compatibility, removed superseded patches, and touched zero renderer targets."
