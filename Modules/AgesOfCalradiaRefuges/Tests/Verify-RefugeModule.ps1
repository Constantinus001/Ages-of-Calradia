param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$moduleXml = Join-Path $ModuleRoot 'SubModule.xml'
$project = Join-Path $ModuleRoot 'AgesOfCalradiaRefuges.csproj'
$subModule = Join-Path $ModuleRoot 'AgesOfCalradiaRefugesSubModule.cs'
$profileCatalog = Join-Path $ModuleRoot 'RefugeSceneProfileCatalog.cs'
$refugeBehavior = Join-Path $ModuleRoot 'CalendarRefugeBehavior.cs'

foreach ($path in @($moduleXml, $project, $subModule, $profileCatalog, $refugeBehavior)) {
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
    'SceneObj\rct_refuge_fort\scene.xscene'
)
foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $ModuleRoot $relativePath) -PathType Leaf)) {
        throw "Required standalone refuge asset is missing: $relativePath"
    }
}

$expectedProfiles = @(
    @{ Climate = 'temperate'; Water = 'plain'; Scene = 'rct_refuge_temperate_land'; Foundation = 'battle_terrain_001' },
    @{ Climate = 'temperate'; Water = 'river'; Scene = 'rct_refuge_temperate_river'; Foundation = 'river_bt_empirewest_01_4x4km' },
    @{ Climate = 'temperate'; Water = 'coast'; Scene = 'rct_refuge_temperate_coast'; Foundation = 'battle_terrain_coastal_02' },
    @{ Climate = 'sturgian'; Water = 'plain'; Scene = 'rct_refuge_snow_land'; Foundation = 'battle_terrain_006' },
    @{ Climate = 'sturgian'; Water = 'river'; Scene = 'rct_refuge_snow_river'; Foundation = 'river_bt_nord_01_4x4km' },
    @{ Climate = 'sturgian'; Water = 'coast'; Scene = 'rct_refuge_snow_coast'; Foundation = 'coastal_terrain_north_of_the_north_sea_01' },
    @{ Climate = 'desert'; Water = 'plain'; Scene = 'rct_refuge_desert_land'; Foundation = 'battle_terrain_009' },
    @{ Climate = 'desert'; Water = 'river'; Scene = 'rct_refuge_desert_river'; Foundation = 'river_bt_aserai_01_4x4km' },
    @{ Climate = 'desert'; Water = 'coast'; Scene = 'rct_refuge_desert_coast'; Foundation = 'battle_terrain_coastal_01' }
)

[xml]$profileManifest = Get-Content -Raw -LiteralPath (Join-Path $ModuleRoot 'ModuleData\RefugeSceneProfiles.xml')
$configuredProfiles = @($profileManifest.refuge_scene_profiles.profile)
if ($configuredProfiles.Count -ne $expectedProfiles.Count) {
    throw "The refuge scene matrix must contain exactly nine profiles; found $($configuredProfiles.Count)."
}

$requiredMarkerTags = @(
    'rct_refuge_anchor',
    'rct_refuge_layout',
    'spawnpoint_player',
    'rct_refuge_steward_spawn',
    'rct_refuge_cook_spawn',
    'rct_refuge_guard_captain_spawn',
    'rct_refuge_healer_spawn'
)

foreach ($expected in $expectedProfiles) {
    $matches = @($configuredProfiles | Where-Object {
        $_.climate -eq $expected.Climate -and $_.water -eq $expected.Water
    })
    if ($matches.Count -ne 1) {
        throw "Missing or duplicate refuge profile: $($expected.Climate)/$($expected.Water)."
    }

    $profile = $matches[0]
    if ($profile.scene_id -ne $expected.Scene -or $profile.foundation_scene -ne $expected.Foundation) {
        throw "Incorrect refuge profile mapping for $($expected.Climate)/$($expected.Water)."
    }

    $sceneDirectory = Join-Path $ModuleRoot (Join-Path 'SceneObj' $expected.Scene)
    foreach ($assetName in @('scene.xscene', 'terrain.bin', 'navmesh.bin')) {
        $assetPath = Join-Path $sceneDirectory $assetName
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "Required refuge scene asset is missing: $($expected.Scene)\$assetName"
        }
    }

    [xml]$sceneDocument = Get-Content -Raw -LiteralPath (Join-Path $sceneDirectory 'scene.xscene')
    if ($sceneDocument.scene.name -ne $expected.Scene) {
        throw "Refuge scene internal name does not match its profile ID: $($expected.Scene)."
    }

    foreach ($tag in $requiredMarkerTags) {
        $taggedEntities = @($sceneDocument.SelectNodes("//game_entity[tags/tag[@name='$tag']]"))
        if ($taggedEntities.Count -ne 1) {
            throw "Refuge scene $($expected.Scene) must contain exactly one entity tagged $tag."
        }
    }
}

$source = Get-Content -Raw -LiteralPath $subModule
if ($source -notmatch 'AddBehavior\(new CalendarRefugeBehavior\(\)\)' -or
    $source -notmatch 'AddBehavior\(new CalendarCampBehavior\(\)\)' -or
    $source -notmatch 'CalendarRefugeIntegration\.RegisterCampOpener' -or
    $source -notmatch 'CalendarRefugeIntegration\.UnregisterCampOpener') {
    throw 'The refuge module must own its behaviors and safely register its optional base-module integration.'
}

$catalogSource = Get-Content -Raw -LiteralPath $profileCatalog
$behaviorSource = Get-Content -Raw -LiteralPath $refugeBehavior
if ($catalogSource -match 'SingleRefugeSceneId' -or
    $behaviorSource -match 'migrated to single-scene mode') {
    throw 'The refuge module must route by the nine climate/access profiles, not a single-scene override.'
}

Write-Output 'PASS: standalone refuge module and all nine climate/access scene profiles are present.'
