# Research-backed refuge improvement

## Recommendation

Finish one module-owned authored refuge scene first:

```text
SceneObj\rct_refuge_temperate_land
```

The scene should contain the linked `rct_refuge_fort_layout` prefab, real
terrain, an `rct_refuge_anchor`, a `spawnpoint_player`, the four staff spawn
tags, and a baked `navmesh.bin`. The campaign now detects that profile only
when all of those files and markers are present. Until then it keeps using the
current `battle_terrain_biome_130` fallback.

This is the safest first improvement because it moves the large fort's
terrain, collision, and navigation into the editor-authored scene instead of
creating the exported hierarchy and its embedded physics at runtime. It also
lets later prefab edits remain linked to the scene rather than rebuilding the
compound from independent runtime props.

## Documentation evidence

TaleWorlds' official modding documentation says that:

- prefabs are reusable template entities and are normally used for mission
  objects and scene props;
- scenes are stored under a module's `SceneObj` folder;
- a linked prefab instance should preserve its connection to the source; and
- scene performance depends on authored occluders, sensible terrain sizing,
  and meshes with appropriate LODs.

Sources:

- [Entities & Prefabs](https://moddocs.bannerlord.com/asset-management/asset-types/prefabs/)
- [Overriding Scenes and Prefabs](https://moddocs.bannerlord.com/asset-management/asset-types/overriding_scenes_prefabs/)
- [Scene Performance Guide](https://moddocs.bannerlord.com/bestpractices/scene_performance_guide/)
- [Scene Spawn Point Guide](https://moddocs.bannerlord.com/authoring-mission-scenes/script-components/scene_spawn_points_guide/)

The official scene guidance also supports the implementation constraints in
our local [Fixed-Refuge-Scene-Authoring](Fixed-Refuge-Scene-Authoring.md)
checklist: author the terrain and navigation around the actual fort, keep the
player spawn on connected navmesh, and validate the final mission in-game.

## What changed in code

`CalendarRefugeMission.IsModuleOwnedSceneReady` performs a fail-closed
capability probe. It requires:

- matching internal and folder scene names;
- `scene.xscene`, `terrain.bin`, and `navmesh.bin`;
- the fort link and all refuge markers; and
- no editor ground-plane entities.

`CalendarRefugeBehavior.GetSceneId` prefers the authored temperate-land
profile only after that probe succeeds. `CalendarRefugeMissionController`
then treats the scene as already containing the fort and skips runtime fort
generation, preventing duplicate geometry and avoiding the known large-prefab
physics crash path.

No scene asset was promoted by this change. The editor workflow must still
produce and visually verify the profile before it becomes active.

## Next implementation step

Use the Scene Editor to complete `rct_refuge_temperate_land`, run
`tools\Test-RefugeSceneProfiles.ps1`, then perform the in-game acceptance
matrix in `docs\Fixed-Refuge-Scene-Authoring.md`. Only after that profile is
stable should the same workflow be repeated for river, coast, desert, and snow
variants.

## Decompiled-mod comparison: patterns to adapt, not copy

The local decompilations of Improved Garrisons and Fourberie were reviewed as
reference material only. No code, asset, or UI text should be copied from
either project. The useful result is a set of engine-behavior clues that can be
reimplemented independently for this mod.

### 1. Defer child dialogs after a picker closes

Improved Garrisons queues actions and runs them from its module tick instead of
opening another modal inquiry directly inside an inquiry callback. That pattern
matches the known Bannerlord problem documented in the local lessons learned:
opening a second modal from a `MultiSelectionInquiry` affirmative callback can
leave the first picker queued and require a second click.

Our steward currently calls `ShowStatus`, `ShowConstructionProjects`,
`OpenStash`, or `OpenGarrison` directly from `HandleMainChoice`. The safest
independent fix is a tiny calendar-owned one-tick action queue, drained from the
submodule or a campaign/UI tick, and routing those four calls through it. The
queued action should re-check that the mission is still active and that the
refuge is still complete before opening UI.

Reference points:

- [Improved Garrisons decompiled queue](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/ImprovedGarrisons_decompiled.txt:15881>)
- [Improved Garrisons deferred inquiry example](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/ImprovedGarrisons_decompiled.txt:22302>)
- [Local inquiry callback lesson](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/lessons_learned.md:1052>)

### 2. Keep the refuge garrison isolated from real party rosters

The decompiled mods use two different party-screen strategies. They provide
real owner parties when managing real campaign parties, but use
`OpenScreenWithCondition` with isolated rosters for temporary or virtual troop
management. This supports keeping the current refuge approach: edit a cloned
garrison roster and commit it only after confirmation. A future manual
`PartyScreenLogicInitializationData` implementation must not assign a real
owner party to an isolated dummy roster, because native initialization can bind
the screen to the real roster and clear it during Cancel.

Reference points:

- [Improved Garrisons isolated/real party screen setup](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/ImprovedGarrisons_decompiled.txt:1677>)
- [Fourberie isolated troop screen usage](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/Fourberie_decompiled.txt:3681>)
- [Local dummy-roster safety rule](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/csharp_patterns.md:100>)

### 3. Do not turn the refuge into a campaign party prematurely

Improved Garrisons creates a `MobileParty` with a custom party component,
custom name, home settlement, owner, clan, and explicit initialization. That
is a valid pattern for a genuine campaign party, but it is not appropriate to
copy into the current refuge: the refuge is intentionally an isolated mission
and has no Settlement or map party. Introducing those objects would enlarge
the save-graph, ownership, AI, and compatibility surface substantially.

If a later design requires a mobile refuge convoy, it should be a separate
feature with its own save schema, lifecycle, disbanding rules, and migration
tests—not a shortcut for the current garrison.

Reference point:

- [Improved Garrisons custom party creation pattern](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/ImprovedGarrisons_decompiled.txt:1268>)

### 4. Keep mission behavior registration explicit

Fourberie opens missions by supplying a deliberate behavior list, including
leave-mission logic and mission-specific systems. This reinforces the current
refuge design: keep refuge interaction and cleanup in explicit
`MissionBehavior` classes, rather than relying on per-frame global searches.
Large static geometry should remain authored in the scene; runtime mission
behaviors should handle interaction, state display, and small dynamic objects.

Reference point:

- [Fourberie mission initialization pattern](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/decom/Fourberie_decompiled.txt:7904>)

## New issues found during this comparison

### High priority: authored-scene save migration

`GetSceneId` now selects `rct_refuge_temperate_land` when the authored profile
is ready, but `IsSceneCompatibleWithWater` does not explicitly recognize that
scene ID. Its final comparison calls `GetSceneId` again, so an old saved refuge
with `battle_terrain_biome_130` can become invalid as soon as the authored
scene is installed. Before publishing the scene, add an explicit compatibility
alias/migration rule and test both directions: old save with new scene
available, and authored save with the profile temporarily unavailable.

### High priority: unpaid garrison upkeep has no consequence

`ChargeGarrisonUpkeep` currently returns when the hero cannot afford the daily
cost. That makes the garrison free during insolvency. Add a serialized unpaid
balance or arrears counter, show it in the refuge summary, and apply a gradual
consequence such as morale loss, desertion, or a temporary capacity lock. The
consequence should be deterministic and bounded so loading a save cannot create
an unexpectedly large debt.

### Medium priority: construction is still on test duration

`RefugeBuildingCatalog` assigns one hour to every upgrade through
`TestConstructionHours`. This is useful during scene testing, but it makes the
construction system materially different from the documented design. Move
durations into named per-building values after scene acceptance, and test
save/load while an upgrade is active.

### Medium priority: deterministic daily fractional rounding

`DailyRateBalance.ScaleDiscreteDailyValue` uses object/hash identity in its
rounding scope. If the scope object changes after load, fractional daily
rounding can differ across reloads. Replace that with a stable serialized key
for refuge-related balances before adding arrears or other cumulative systems.
