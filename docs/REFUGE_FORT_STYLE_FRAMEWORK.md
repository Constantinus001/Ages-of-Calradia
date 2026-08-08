# Refuge Fort Style Framework

## Player flow

1. Camp menu: select **Build a refuge**.
2. The campaign site survey determines climate and land/river/coast access.
3. A native multi-selection picker displays the three fort styles.
4. A release style is selectable only when its prefab and the exact authored scene for that climate/access combination pass validation. A manifest entry may explicitly opt into the temporary native test fallback.
5. The selected prefab ID and scene ID are saved before construction starts.
6. On completion, entering the refuge opens the selected fixed authored scene. The fort is a single linked prefab root in that scene.

The player may dismantle an empty refuge from its refuge-status menu and found another one with a different style. Dismantling removes this framework's tracked campaign-map markers immediately. If Bannerlord recreates the map scene during a load/refresh, the framework detects the new scene and recreates only its own marker cluster; it does not force an unsafe whole-map reload.

## Registered styles

| Player name | Prefab root ID | Asset file | Scene suffix |
|---|---|---|---|
| Palisade Ring | `rct_refuge_fort_layout` | `Prefabs\rct_refuge_fort_layout.xml` | none |
| Hill Fort (test) | `rct_refuge_fort_hill` | `Prefabs\rct_refuge_fort_layout.xml` (temporary alias) | `_hill` |
| Riverhold (test) | `rct_refuge_fort_river` | `Prefabs\rct_refuge_fort_layout.xml` (temporary alias) | `_riverhold` |

For the current test build, all three choices intentionally alias the known Palisade Ring prefab. Their style IDs, save records, picker entries, and scene suffixes remain distinct, so the full campaign flow can be tested before unique art exists. Replace the `prefab_file` and `linked_prefab_root` values for Hill Fort and Riverhold when their original prefab assets are ready.

## Drop-in configuration

`ModuleData\RefugeFortStyles.xml` remains the backwards-compatible registry. The preferred organization is one drop-in manifest per style:

```text
ModuleData\RefugeStyles\PalisadeRing\style.xml
ModuleData\RefugeStyles\HillFort\style.xml
ModuleData\RefugeStyles\Riverhold\style.xml
```

Those manifests override registry entries with the same ID. A fort entry specifies its saved style ID, prefab file, linked prefab root, display text, scene suffix, and whether the legacy native terrain path is allowed for testing.

Keep live world-prefab XML files directly under `Prefabs\`. This mirrors the reliable Homesteads-style registry pattern while avoiding an unverified nested-prefab loader assumption. A style folder owns its definition, source notes, and eventually its editor scene work; it does not require moving the runtime XML asset.

For a new custom fort:

1. Drag/drop its complete XML prefab into `Prefabs`.
2. Ensure its root ID starts `rct_refuge_fort_` and has the `rct_refuge_layout` tag.
3. Add a `<fort ... />` entry to `ModuleData\RefugeFortStyles.xml` with a unique `scene_suffix`.
4. Create and bake the matching authored scenes.
5. Change `allow_native_test_fallback` to `false` before release.

Valid dropped `rct_refuge_fort_*.xml` files are also discovered automatically. The manifest is how you give them a polished name, description, suffix, and test-mode rule.

`ModuleData\RefugeSceneProfiles.xml` is the configurable nine-map matrix. It names every climate/access scene and records the recommended Native editor foundation. Use `sturgian` and `plain` for readable configuration; the runtime maps those to the established `snow` and `land` scene IDs.

## Required scene IDs

Every style needs nine scenes: three climates by three water profiles. Examples:

| Style | Temperate land | Desert river | Snow coast |
|---|---|---|---|
| Palisade Ring | `rct_refuge_temperate_land` | `rct_refuge_desert_river` | `rct_refuge_snow_coast` |
| Hill Fort | `rct_refuge_temperate_land_hill` | `rct_refuge_desert_river_hill` | `rct_refuge_snow_coast_hill` |
| Riverhold | `rct_refuge_temperate_land_riverhold` | `rct_refuge_desert_river_riverhold` | `rct_refuge_snow_coast_riverhold` |

This gives 27 possible fixed authored scene profiles. Build the first one for each style before expanding to all water/climate variants.

## Compatibility contract for each fort prefab

- Root `<game_entity name>` must exactly equal its registered prefab ID.
- The root must have the `rct_refuge_layout` tag.
- Keep the placed scene instance linked. Do not unpack it or alter child transforms in individual scenes.
- The scene must contain a separate empty `rct_refuge_anchor` at the same position and rotation as the prefab root, with scale `1,1,1`.
- Each scene needs its own terrain, collision, player/staff spawn markers, and baked navmesh.
- Never instantiate the current large fort root directly at mission runtime. It previously caused native access violations. Scene-linked loading is the supported path. `allow_native_test_fallback` is for verifying the picker/save/dismantle flow only; it does not replace authored collision/navmesh.

Run `tools\Test-RefugeSceneProfiles.ps1` after every prefab or scene change. It validates all 27 profiles against these requirements.
Run `tools\Get-RefugeSceneAuthoringQueue.ps1` before opening the editor to see the nine-map foundation and file-status queue from the same configuration manifest.
