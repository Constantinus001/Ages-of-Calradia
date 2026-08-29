param(
    [string]$BannerlordDir = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ReligionDll = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\Win64_Shipping_Client\AgesOfCalradiaReligions.dll')
)
$ErrorActionPreference = 'Stop'
$coreBin = Join-Path $BannerlordDir 'Modules\Ages Of Calradia\bin\Win64_Shipping_Client'

[Reflection.Assembly]::LoadFrom((Join-Path $coreBin '0Harmony.dll')) | Out-Null
foreach ($name in @(
    'TaleWorlds.Library.dll', 'TaleWorlds.DotNet.dll', 'TaleWorlds.Engine.dll',
    'TaleWorlds.InputSystem.dll', 'TaleWorlds.GauntletUI.dll', 'TaleWorlds.GauntletUI.Data.dll',
    'TaleWorlds.Engine.GauntletUI.dll', 'TaleWorlds.ScreenSystem.dll', 'TaleWorlds.Core.dll',
    'TaleWorlds.Localization.dll', 'TaleWorlds.ObjectSystem.dll', 'TaleWorlds.SaveSystem.dll',
    'TaleWorlds.CampaignSystem.dll', 'TaleWorlds.MountAndBlade.dll',
    'TaleWorlds.Core.ViewModelCollection.dll', 'TaleWorlds.CampaignSystem.ViewModelCollection.dll'
)) {
    [Reflection.Assembly]::LoadFrom((Join-Path $BannerlordDir "bin\Win64_Shipping_Client\$name")) | Out-Null
}

[Reflection.Assembly]::LoadFrom((Join-Path $coreBin 'AgesOfCalradia.dll')) | Out-Null
$religion = [Reflection.Assembly]::LoadFrom($ReligionDll)
$type = $religion.GetType('AgesOfCalradiaReligions.StrategicMapModeIntegration', $true)
$install = $type.GetMethod('Install', [Reflection.BindingFlags]'Static,NonPublic')
$reset = $type.GetMethod('Reset', [Reflection.BindingFlags]'Static,NonPublic')
$buildMarkers = $type.GetMethod('BuildDemographicMarkerSequence', [Reflection.BindingFlags]'Static,NonPublic')
$selectMode = $type.GetMethod('SelectMode', [Reflection.BindingFlags]'Static,NonPublic')
$atlasPrefix = $type.GetMethod('BeforeAtlasUpdate', [Reflection.BindingFlags]'Static,NonPublic')
$harmony = [Activator]::CreateInstance([HarmonyLib.Harmony], [object[]]@('aoc.mapmodes.contracttest'))
try {
    $install.Invoke($null, [object[]]@($harmony)) | Out-Null
    $core = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq 'AgesOfCalradia' } | Select-Object -First 1
    $pointType = $core.GetType('TwelveMonthCalendar.StrategicSettlementPoint', $true)
    $listType = [Collections.Generic.List``1].MakeGenericType($pointType)
    $emptyMarkers = [Activator]::CreateInstance($listType)
    $markerArguments = New-Object object[] 1
    $markerArguments[0] = $emptyMarkers
    $neutralMarkers = $buildMarkers.Invoke($null, $markerArguments)
    if ($null -eq $neutralMarkers -or $neutralMarkers.GetType() -ne $listType) {
        throw 'Demographic marker neutralization does not satisfy the live atlas marker contract.'
    }
    $modeType = $religion.GetType('AgesOfCalradiaReligions.StrategicMapMode', $true)
    $cultureMode = [Enum]::Parse($modeType, 'Culture')
    $modeArguments = New-Object object[] 1
    $modeArguments[0] = $cultureMode
    $selectMode.Invoke($null, $modeArguments) | Out-Null
    $politicalColours = New-Object 'System.Collections.Generic.Dictionary[string,uint32]'
    $politicalColours.Add('town_A1', [uint32]::Parse('FF965228', [Globalization.NumberStyles]::HexNumber))
    $atlasArguments = [Array]::CreateInstance([object], 2)
    $atlasArguments.SetValue($politicalColours, 0)
    $atlasArguments.SetValue($emptyMarkers, 1)
    $prefixArguments = [Array]::CreateInstance([object], 1)
    $prefixArguments.SetValue($atlasArguments, 0)
    $atlasPrefix.Invoke($null, $prefixArguments) | Out-Null
    $submittedColours = $atlasArguments[0]
    if ($submittedColours['town_A1'] -ne [uint32]::Parse('FF5B5147', [Globalization.NumberStyles]::HexNumber)) {
        throw 'Harmony live argument zero was not replaced by the neutral demographic canvas.'
    }
    Write-Host 'Runtime Harmony contract verification passed.'
}
finally {
    $harmony.UnpatchAll('aoc.mapmodes.contracttest')
    $reset.Invoke($null, @()) | Out-Null
}
