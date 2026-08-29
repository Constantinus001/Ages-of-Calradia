param([string]$PackageRoot)

$ErrorActionPreference = 'Stop'
$systemsRoot = Split-Path -Parent $PSScriptRoot
$modulesRoot = Split-Path -Parent $systemsRoot
[xml]$manifest = Get-Content -LiteralPath (Join-Path $systemsRoot 'SubModule.xml') -Raw

if ($manifest.Module.Name.value -ne 'AOC SYSTEMS L & R' -or
    $manifest.Module.Id.value -ne 'AgesOfCalradiaSystemsLR' -or
    $manifest.Module.Version.value -ne 'v1.0.0') {
    throw 'L & R manifest name, ID, or version is incorrect.'
}

$expected = @(
    @{ Name = 'AOC SYSTEMS L & R: Logistics'; DLL = 'AgesOfCalradiaLogistics.dll'; Class = 'AgesOfCalradiaLogistics.LogisticsSubModule' },
    @{ Name = 'AOC SYSTEMS L & R: Refuges'; DLL = 'AgesOfCalradiaRefuges.dll'; Class = 'AgesOfCalradiaRefuges.AgesOfCalradiaRefugesSubModule' }
)
$actual = @($manifest.Module.SubModules.SubModule)
if ($actual.Count -ne 2) { throw "L & R must declare exactly two submodules; found $($actual.Count)." }
for ($index = 0; $index -lt 2; $index++) {
    if ($actual[$index].Name.value -ne $expected[$index].Name -or
        $actual[$index].DLLName.value -ne $expected[$index].DLL -or
        $actual[$index].SubModuleClassType.value -ne $expected[$index].Class) {
        throw "Incorrect L & R submodule at position $($index + 1)."
    }
}

$dependencies = @($manifest.Module.DependedModules.DependedModule | ForEach-Object { $_.Id })
if ($dependencies -notcontains 'AgesOfCalradia') { throw 'L & R must load after AOC CORE.' }
$incompatible = @($manifest.Module.IncompatibleModules.Module | ForEach-Object { $_.Id })
foreach ($id in @('AgesOfCalradiaSystems', 'AgesOfCalradiaLogistics', 'AgesOfCalradiaRefuges')) {
    if ($incompatible -notcontains $id) { throw "L & R must reject duplicate module identity: $id" }
}

$xmlNodes = @($manifest.Module.Xmls.XmlNode)
if (@($xmlNodes | Where-Object { $_.XmlName.id -eq 'Items' -and $_.XmlName.path -eq 'supply_items' }).Count -ne 1 -or
    @($xmlNodes | Where-Object { $_.XmlName.id -eq 'GameText' -and $_.XmlName.path -eq 'module_strings' }).Count -ne 1) {
    throw 'L & R must register Logistics items and game text.'
}

$refuge = Get-Content -LiteralPath (Join-Path $modulesRoot 'AgesOfCalradiaRefuges\CalendarRefugeBehavior.cs') -Raw
$logistics = Get-Content -LiteralPath (Join-Path $modulesRoot 'AgesOfCalradiaLogistics\LogisticsReserveBehavior.cs') -Raw
foreach ($token in @('AgesOfCalradia.RefugeV2', 'AgesOfCalradia.RefugeStashV2', 'AgesOfCalradia.RefugeGarrisonV2')) {
    if ($refuge -notmatch [regex]::Escape($token)) { throw "Refuge save key changed: $token" }
}
if ($logistics -notmatch 'aoc_logistics_reserves') { throw 'Logistics reserve save key changed.' }

if (-not [string]::IsNullOrWhiteSpace($PackageRoot)) {
    [xml]$packageManifest = Get-Content -LiteralPath (Join-Path $PackageRoot 'SubModule.xml') -Raw
    if ($packageManifest.Module.Id.value -ne 'AgesOfCalradiaSystemsLR') { throw 'Packaged L & R manifest is incorrect.' }
    foreach ($path in @(
        'bin\Win64_Shipping_Client\AgesOfCalradiaLogistics.dll',
        'bin\Win64_Shipping_Client\AgesOfCalradiaRefuges.dll',
        'ModuleData\supply_items.xml',
        'ModuleData\module_strings.xml',
        'ModuleData\RefugeSceneProfiles.xml',
        'GUI\Prefabs\RefugeBuilderHud.xml',
        'Prefabs\rct_refuge_fort_runtime_layout.xml',
        'SceneObj\rct_refuge_fort\scene.xscene')) {
        if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot $path) -PathType Leaf)) { throw "Packaged L & R file is missing: $path" }
    }
    if (Test-Path -LiteralPath (Join-Path $PackageRoot 'SceneObj\rct_refuge_collision_navmesh_workshop')) {
        throw 'Refuge workshop scene entered the L & R runtime package.'
    }
    $developmentFiles = @(Get-ChildItem -LiteralPath $PackageRoot -File -Recurse | Where-Object { $_.Extension -in @('.pdb', '.txt') })
    if ($developmentFiles.Count -gt 0) { throw "Development files entered L & R: $($developmentFiles.FullName -join ', ')" }
}

Write-Output 'PASS: AOC SYSTEMS L & R manifest, assets, incompatibilities, and save contracts verified.'
