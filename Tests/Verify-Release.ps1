param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ReleaseArchive
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
$harmonyDll = Join-Path $ModuleRoot 'bin\Win64_Shipping_Client\0Harmony.dll'
$moduleXml = Join-Path $ModuleRoot 'SubModule.xml'
$readme = Join-Path $ModuleRoot 'README.md'
$mapBarXml = Join-Path $ModuleRoot 'GUI\Prefabs\Map\MapBar.xml'
$optionsXml = Join-Path $ModuleRoot 'GUI\Prefabs\Options\SPOptions\Options.xml'
$runtimeFiles = @($moduleXml, $readme, $harmonyDll, $mainDll, $mcmDll, $mapBarXml, $optionsXml)
foreach ($path in $runtimeFiles) {
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

[xml]$manifest = Get-Content -Raw -LiteralPath $moduleXml
$version = $manifest.Module.Version.value
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'SubModule.xml does not define a module version.'
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
        (Join-Path $moduleStage 'GUI\Prefabs\Map'), `
        (Join-Path $moduleStage 'GUI\Prefabs\Options\SPOptions') | Out-Null
    Copy-Item -LiteralPath $moduleXml, $readme -Destination $moduleStage
    Copy-Item -LiteralPath $harmonyDll, $mainDll, $mcmDll -Destination (Join-Path $moduleStage 'bin\Win64_Shipping_Client')
    Copy-Item -LiteralPath $mapBarXml -Destination (Join-Path $moduleStage 'GUI\Prefabs\Map')
    Copy-Item -LiteralPath $optionsXml -Destination (Join-Path $moduleStage 'GUI\Prefabs\Options\SPOptions')
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
    '_TwelveMonthCalendar/GUI/Prefabs/Map/MapBar.xml',
    '_TwelveMonthCalendar/GUI/Prefabs/Options/SPOptions/Options.xml'
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
Start-Sleep -Seconds 2
$escapedArchive = [Regex]::Escape($ReleaseArchive)
$detections = Get-MpThreatDetection | Where-Object {
    $_.Resources -match $escapedArchive -and $_.InitialDetectionTime -ge $scanStarted.AddMinutes(-1)
}
if ($detections) {
    $detections | Format-List | Out-String | Write-Error
    throw 'Microsoft Defender detected a threat in the release archive. Do not upload this release.'
}

$archiveHash = (Get-FileHash $ReleaseArchive -Algorithm SHA256).Hash
Write-Output ('PASS: Defender scan clean; Archive={0}; SHA256={1}; DailyFactor={2:F8}; DurationFactor={3:F8}; MainDLL={4}; MCMDLL={5}' -f `
    $ReleaseArchive, $archiveHash, $dailyFactor, $durationFactor, (Get-FileHash $mainDll -Algorithm SHA256).Hash, (Get-FileHash $mcmDll -Algorithm SHA256).Hash)
