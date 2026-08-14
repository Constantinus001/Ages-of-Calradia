param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$moduleXml = Join-Path $ModuleRoot 'SubModule.xml'
$project = Join-Path $ModuleRoot 'AgesOfCalradiaRefuges.csproj'
$subModule = Join-Path $ModuleRoot 'AgesOfCalradiaRefugesSubModule.cs'

foreach ($path in @($moduleXml, $project, $subModule)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required refuge-module file is missing: $path"
    }
}

[xml]$manifest = Get-Content -Raw -LiteralPath $moduleXml
if ($manifest.Module.Id.value -ne 'AgesOfCalradiaRefuges' -or
    $manifest.Module.SubModules.SubModule.DLLName.value -ne 'AgesOfCalradiaRefuges.dll') {
    throw 'The refuge module manifest does not identify its standalone assembly.'
}

$dependencies = @($manifest.Module.DependedModules.DependedModule | ForEach-Object { $_.Id })
if ($dependencies -notcontains 'AgesOfCalradia') {
    throw 'The standalone refuge module must load after Ages of Calradia.'
}

$requiredPaths = @(
    'GUI\Prefabs\RefugeBuilderHud.xml',
    'ModuleData\RefugeAnchors\verified_builtin_anchors.xml',
    'ModuleData\RefugeSceneProfiles.xml',
    'ModuleData\RefugeFortStyles.xml',
    'Prefabs\rct_refuge_fort_layout.xml',
    'Prefabs\rct_refuge_fort_runtime_layout.xml',
    'SceneObj\rct_refuge_fort\scene.xscene',
    'SceneObj\rct_refuge_temperate_land\scene.xscene',
    'SceneObj\rct_refuge_desert_land\scene.xscene',
    'SceneObj\rct_refuge_snow_land\scene.xscene'
)
foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $ModuleRoot $relativePath) -PathType Leaf)) {
        throw "Required standalone refuge asset is missing: $relativePath"
    }
}

$source = Get-Content -Raw -LiteralPath $subModule
if ($source -notmatch 'AddBehavior\(new CalendarRefugeBehavior\(\)\)' -or
    $source -notmatch 'AddBehavior\(new CalendarCampBehavior\(\)\)' -or
    $source -notmatch 'CalendarRefugeIntegration\.RegisterCampOpener' -or
    $source -notmatch 'CalendarRefugeIntegration\.UnregisterCampOpener') {
    throw 'The refuge module must own its behaviors and safely register its optional base-module integration.'
}

Write-Output 'PASS: standalone refuge module manifest, integration, and required runtime assets are present.'
