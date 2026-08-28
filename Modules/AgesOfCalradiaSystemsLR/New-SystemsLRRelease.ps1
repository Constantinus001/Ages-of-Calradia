param([string]$OutputRoot)

$ErrorActionPreference = 'Stop'
$systemsSource = $PSScriptRoot
$modulesRoot = Split-Path -Parent $systemsSource
$repositoryRoot = Split-Path -Parent $modulesRoot
[xml]$manifest = Get-Content -LiteralPath (Join-Path $systemsSource 'SubModule.xml') -Raw
$version = [string]$manifest.Module.Version.value
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot ("artifacts\AgesOfCalradiaSystemsLR-{0}" -f $version)
}

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot)
$safePrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($safePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "L & R output must remain inside: $artifactsRoot"
}
if (Test-Path -LiteralPath $resolvedOutput) { Remove-Item -LiteralPath $resolvedOutput -Recurse -Force }

$moduleRoot = Join-Path $resolvedOutput 'Modules\AgesOfCalradiaSystemsLR'
$binaryRoot = Join-Path $moduleRoot 'bin\Win64_Shipping_Client'
New-Item -ItemType Directory -Path $binaryRoot -Force | Out-Null

function Invoke-FeatureBuild([string]$Project, [string[]]$Properties = @()) {
    $arguments = @('msbuild', $Project, '/t:Rebuild', '/p:Configuration=Release', "/p:OutputPath=$binaryRoot", '/v:minimal') + $Properties
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "L & R Release build failed for $Project with exit code $LASTEXITCODE." }
}
function Copy-TreeContents([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $Destination -Recurse -Force
}

$logisticsRoot = Join-Path $modulesRoot 'AgesOfCalradiaLogistics'
$refugesRoot = Join-Path $modulesRoot 'AgesOfCalradiaRefuges'
Invoke-FeatureBuild (Join-Path $logisticsRoot 'AgesOfCalradiaLogistics.csproj')
Invoke-FeatureBuild (Join-Path $refugesRoot 'AgesOfCalradiaRefuges.csproj') @("/p:BaseModuleDirectory=$repositoryRoot")

foreach ($dll in @('AgesOfCalradiaLogistics.dll', 'AgesOfCalradiaRefuges.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $binaryRoot $dll) -PathType Leaf)) { throw "L & R assembly is missing: $dll" }
}
Get-ChildItem -LiteralPath $binaryRoot -Filter '*.pdb' -File | Remove-Item -Force
Copy-Item -LiteralPath (Join-Path $systemsSource 'SubModule.xml') -Destination $moduleRoot -Force
Copy-Item -LiteralPath (Join-Path $systemsSource 'README.md') -Destination $moduleRoot -Force

foreach ($directory in @('ModuleData', 'GUI', 'Prefabs', 'SceneObj')) {
    Copy-TreeContents (Join-Path $refugesRoot $directory) (Join-Path $moduleRoot $directory)
}
$workshopScene = Join-Path $moduleRoot 'SceneObj\rct_refuge_collision_navmesh_workshop'
if (Test-Path -LiteralPath $workshopScene) { Remove-Item -LiteralPath $workshopScene -Recurse -Force }
Get-ChildItem -LiteralPath (Join-Path $moduleRoot 'SceneObj') -File -Recurse |
    Where-Object { $_.Extension -eq '.txt' -or $_.Name -eq 'README.md' } |
    Remove-Item -Force

$moduleData = Join-Path $moduleRoot 'ModuleData'
Copy-Item -LiteralPath (Join-Path $logisticsRoot 'ModuleData\supply_items.xml') -Destination $moduleData -Force
Copy-Item -LiteralPath (Join-Path $logisticsRoot 'ModuleData\module_strings.xml') -Destination $moduleData -Force

& (Join-Path $systemsSource 'Tests\Verify-SystemsLRModule.ps1') -PackageRoot $moduleRoot
if ($LASTEXITCODE -ne 0) { throw 'L & R package verification failed.' }
$archive = "$resolvedOutput.zip"
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -LiteralPath (Join-Path $resolvedOutput 'Modules') -DestinationPath $archive -CompressionLevel Optimal
Write-Output "PASS: AOC SYSTEMS L & R package: $moduleRoot"
Write-Output "PASS: AOC SYSTEMS L & R archive: $archive"
