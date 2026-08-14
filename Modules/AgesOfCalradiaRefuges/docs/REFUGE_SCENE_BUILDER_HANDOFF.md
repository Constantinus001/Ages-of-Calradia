# Refuge Scene Builder Handoff

## Objective

Finish the fixed refuge mission scenes used by the Ages of Calradia refuge
system. The campaign map surveys the site dynamically, but the mission itself
loads one pre-authored `SceneObj` profile; it does not reproduce the campaign
terrain at run time.

The profile is selected from the site's climate and water access:

- climate: temperate, Sturgian/snow, or desert;
- access: land, river, or coast.

The source of truth is `ModuleData/RefugeSceneProfiles.xml`. Do not rename its
scene IDs without a matching code and manifest change.

## Required default-style scene matrix

Create or finish these nine default **Palisade Ring** scenes. Their directory
name, internal `<scene name="...">`, and profile `scene_id` must match exactly.

| Profile | Scene ID | Native terrain foundation |
| --- | --- | --- |
| Temperate land | `rct_refuge_temperate_land` | `battle_terrain_001` |
| Temperate river | `rct_refuge_temperate_river` | `river_bt_empirewest_01_4x4km` |
| Temperate coast | `rct_refuge_temperate_coast` | `battle_terrain_coastal_02` |
| Snow land | `rct_refuge_snow_land` | `battle_terrain_006` |
| Snow river | `rct_refuge_snow_river` | `river_bt_nord_01_4x4km` |
| Snow coast | `rct_refuge_snow_coast` | `coastal_terrain_north_of_the_north_sea_01` |
| Desert land | `rct_refuge_desert_land` | `battle_terrain_009` |
| Desert river | `rct_refuge_desert_river` | `river_bt_aserai_01_4x4km` |
| Desert coast | `rct_refuge_desert_coast` | `battle_terrain_coastal_01` |

Each scene belongs in `SceneObj/<scene ID>/` and must include, at minimum:

- `scene.xscene`
- editor-authored `terrain.bin`
- baked `navmesh.bin`
- the appropriate supporting scene files produced by the editor (for example,
  atmosphere, flora, and flowmap data when applicable)

## Fort layout and marker contract

Each default-style scene must contain one linked instance of the prefab root
`rct_refuge_fort_layout` from `Prefabs/rct_refuge_fort_layout.xml`.

The scene validator requires all of the following:

- Exactly one entity tagged `rct_refuge_anchor`.
- Exactly one separate entity tagged `rct_refuge_layout`, attached to the
  `rct_refuge_fort_layout` root.
- The anchor and layout root positioned within **0.5 m** of each other, with
  the same rotation.
- Layout-root scale of `1,1,1`.
- One player start marker: `spawnpoint_player`.
- Staff spawn entities named, prefabricated, or tagged as:
  - `rct_refuge_steward_spawn`
  - `rct_refuge_cook_spawn`
  - `rct_refuge_guard_captain_spawn`
  - `rct_refuge_healer_spawn`
- No `editor_plane_low` or `bo_editor_plane` entity left in the saved scene.

Put the fort compound on a dry, reachable pad. Navmesh and collision must be
rebaked only after the fort, props, terrain, and water boundary are final.
Verify player-to-staff paths, access through the entrance, and that agents
cannot fall through or path into water/cliffs.

## Current asset status

The repository currently has working folders/templates for the six river and
coast default profiles. The three land profiles are still required:

- `rct_refuge_temperate_land`
- `rct_refuge_snow_land`
- `rct_refuge_desert_land`

Existing folders can be used as authoring references, but they are not assumed
release-ready until the validator passes.

## Editor workflow

1. Copy or create the scene folder under `SceneObj` using the exact ID.
2. Base the terrain, water, atmosphere, and vegetation on the foundation scene
   in the matrix.
3. Place the complete refuge prefab as one linked layout root; do not rebuild
   it from independently runtime-spawned walls or towers.
4. Add the anchor, player start, and four staff markers.
5. Finalize collision, then bake the terrain and navmesh in the Bannerlord
   Scene Editor.
6. Save all generated scene artifacts in the module-owned scene folder.
7. Run the validation command below. Correct every reported issue before
   marking a scene complete.

```powershell
& .\tools\Test-RefugeSceneProfiles.ps1
```

## Runtime behavior to test

Founding is based on the campaign site's detected profile:

- snow terrain or Sturgian area -> snow profile;
- desert/dune terrain or Aserai area -> desert profile;
- all other sites -> temperate profile;
- nearby verified river -> river profile;
- nearby navigable coastal/open sea -> coast profile;
- otherwise -> land profile.

In game, found/visit one refuge for every completed profile. Confirm the right
terrain loads, the fort appears exactly once at its anchor, the player and all
four staff members spawn on reachable navmesh, and exiting returns safely to
the campaign map.

## Acceptance criteria

- `tools/Test-RefugeSceneProfiles.ps1` exits with code 0.
- The Release build succeeds.
- All nine Palisade Ring profiles are complete.
- No native test fallback is required for a finished profile.
