param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BannerlordDir = 'C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord'
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

$calendarAssembly = [Reflection.Assembly]::LoadFrom(
    (Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\TwelveMonthCalendar.dll'))
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

Assert-Equal $true (Invoke-CalendarMath 'IsLeapYear' @(1084)) 'Leap year 1084'
Assert-Equal $false (Invoke-CalendarMath 'IsLeapYear' @(1100)) 'Century year 1100'
Assert-Equal $true (Invoke-CalendarMath 'IsLeapYear' @(1200)) 'Four-hundred-year 1200'
Assert-Equal 366 (Invoke-CalendarMath 'GetYearLength' @(1084)) 'Leap-year length'
Assert-Equal 365 (Invoke-CalendarMath 'GetYearLength' @(1085)) 'Common-year length'
Assert-Equal 80 (Invoke-CalendarMath 'GetSeasonStartDayOfYear' @(1084, 0)) 'Leap-year spring boundary'
Assert-Equal 79 (Invoke-CalendarMath 'GetSeasonStartDayOfYear' @(1085, 0)) 'Common-year spring boundary'

Write-Output 'PASS: Calendar math leap-year, year-length, and season-boundary checks passed.'
