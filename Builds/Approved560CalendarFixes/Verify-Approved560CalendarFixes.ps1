param(
    [string]$BannerlordDir = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ModuleRoot = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Ages Of Calradia',
    [string]$SidecarPath = (Join-Path $PSScriptRoot 'bin\Win64_Shipping_Client\AgesOfCalradia.Approved560CalendarFixes.dll')
)

$ErrorActionPreference = 'Stop'
$gameBin = Join-Path $BannerlordDir 'bin\Win64_Shipping_Client'
$moduleBin = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client'
[Reflection.Assembly]::LoadFrom((Join-Path $moduleBin '0Harmony.dll')) | Out-Null
foreach ($name in @(
    'TaleWorlds.Library.dll',
    'TaleWorlds.DotNet.dll',
    'TaleWorlds.Engine.dll',
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
if ($count -lt 14) {
    throw "Expected at least 14 compatibility patches; found $count."
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

Write-Output "PASS: approved main + sidecar load registered $count fixes, removed superseded patches, and touched zero renderer targets."
