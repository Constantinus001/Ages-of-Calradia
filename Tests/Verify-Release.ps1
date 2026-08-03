param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$mainProject = Join-Path $ModuleRoot 'TwelveMonthCalendar.csproj'
$mcmProject = Join-Path $ModuleRoot 'TwelveMonthCalendar.MCM.csproj'
dotnet msbuild $mainProject /t:Rebuild /p:Configuration=Release /v:minimal
dotnet msbuild $mcmProject /t:Rebuild /p:Configuration=Release /v:minimal

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

$mainDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\TwelveMonthCalendar.dll'
$mcmDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\TwelveMonthCalendar.MCM.dll'
foreach ($path in @($mainDll, $mcmDll)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Expected release output is missing: $path"
    }
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

Write-Output ('PASS: DailyFactor={0:F8}; DurationFactor={1:F8}; MainDLL={2}; MCMDLL={3}' -f `
    $dailyFactor, $durationFactor, (Get-FileHash $mainDll -Algorithm SHA256).Hash, (Get-FileHash $mcmDll -Algorithm SHA256).Hash)
