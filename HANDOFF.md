# Ages of Calradia - Refuge System Handoff

Updated: 2026-08-07

## Immediate objective

Load the player's authored refuge fort on the exact open-plains terrain shown in the reference screenshot with the Refuge Builder visible. Do not replace the terrain based on appearance or timestamp guesses.

The confirmed working terrain configuration is:

- Scene: `battle_terrain_biome_130`
- `NeedsRandomTerrain = true`
- `RandomTerrainSeed = 10840415`
- `TerrainType = TerrainType.Plain`
- Calibrated layout center from successful logs: approximately `1622.88, 765.07`

The screenshot target is the broad tan-green clearing with sparse trees around the outer area. It is not the dense forest, water/editor plane, or biome-015 scene.

## Project locations

- Source: `D:\AI-Related Apllications & Modding\Modding\Bannerlord Modding Stuff\_TwelveMonthCalendar`
- Installed module: `C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\AgesOfCalradia`
- Runtime log: `C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\AgesOfCalradia\Logs\AgesOfCalradia.log`
- Main project: `TwelveMonthCalendar.csproj`
- Built DLL: `bin\Win64_Shipping_Client\AgesOfCalradia.dll`

## Current deployed build

The v1.5.2 release was compiled and deployed while Bannerlord was closed.

- Build result: 0 warnings, 0 errors
- Deployed main DLL SHA-256: `8D8AAE63416180A7894570A9E7980EC8818BC7F5D25AE3DE123088F7AF6B1476`
- Deployed MCM DLL SHA-256: `C326405EEAC277CE1E4F5D385745B06FF94B55E7226F3E4A14F73875CFC512AE`
- The module-root and `bin\Win64_Shipping_Client` main DLLs match the verified release payload.
- 166 release files were synchronized from `artifacts\AgesOfCalradia-v1.5.2.zip`.
- Rollback copy: `Backups\Deploy-20260807-163000`
- Release verification and Defender cloud-verdict hold completed successfully.

The deployment still needs in-game campaign verification for the clock,
economy recovery, caravan routing, and strategy-map display.

## Why the forest kept appearing

Changing only the scene ID was not enough. `ApplyCampaignEnvironment` copied the terrain type from the party's campaign-map face. If the party stood on a forest face, Bannerlord could generate the biome-130 mission as forest terrain.

`CalendarRefugeMission.TryOpen` now applies campaign atmosphere first and then overrides generated refuge terrain with:

```csharp
initializer.TerrainType = (int)TerrainType.Plain;
```

That override is applied only when the selected scene needs generated biome terrain. Do not remove it while reproducing the reference screenshot.

## Scene routing

`CalendarRefugeBehavior.GetSceneId` currently sends every refuge climate/water profile to the proven temperate plains foundation:

`battle_terrain_biome_130`

This is temporary while the base refuge is stabilized. Water access still remains campaign data and controls ship storage eligibility.

Existing refuges are also redirected at entry time. `TryEnterCompletedRefuge` reads the saved climate but calls `GetSceneId` again before opening the mission, so an old saved forest scene ID should not force the old scene.

Do not substitute:

- `battle_terrain_001`: produced the unwanted forest test.
- `battle_terrain_015`: used a different calibrated anchor and is not the reference screenshot.
- `rct_refuge_fort`: editor workspace with test ground/water, not the playable refuge terrain.
- `forest_hideout_003`: legacy forest hideout scene.

## Authored fort prefab

The intended fort is:

`Prefabs\rct_refuge_fort_layout.xml`

Known properties:

- Prefab ID: `rct_refuge_fort_layout`
- Size: 143,951 bytes
- SHA-256: `BD0062D06795802CFF39321917373C6C5F7CD3128B9AF25D9474AF03969B39DE`
- 192 game entities
- 120 embedded physics definitions
- No `editor_plane_low` or `bo_editor_plane`
- Original exported copy: `C:\Users\fpicc\Desktop\rct_refuge_fort_layout.xml`

The prefab is instantiated as one hierarchy at the calibrated biome-130 anchor so its authored spacing and rotations remain intact.

## Native crash history and current safety measure

The authored prefab previously caused `System.AccessViolationException` inside Bannerlord's native prefab creation calls. Both the initial-frame prefab overload and creation of all embedded physics bodies at once were unstable.

The current `InstantiateRefugePrefab` path creates this large authored fort with embedded physics disabled, then sets its global frame after creation. Do not restore the unsafe initial-frame overload or enable all 120 physics bodies together.

This means the fort may initially be visual-only. Add collision later through a small number of verified native collision proxies or individually validated collision-bearing props. Stability takes priority.

## Relevant implementation files

- Campaign state, construction, scene selection, and entry: `CalendarRefugeBehavior.cs`
- Mission initialization, terrain generation, spawn anchor, fort placement, staff, and exit handling: `CalendarRefugeMission.cs`
- Runtime layout builder: `CalendarRefugeLayoutBuilderBehavior.cs`
- Builder HUD logic: `CalendarRefugeBuilderHudView.cs`
- Builder HUD XML: `GUI\Prefabs\RefugeBuilderHud.xml`
- Placeable asset catalog: `RefugeBuildingCatalog.cs`
- Steward interaction: `CalendarRefugeStewardInteraction.cs`
- Upgrade state: `RefugeUpgrade.cs`

## Immediate test procedure

1. Start Bannerlord after confirming no old game instance remains in memory.
2. Enable Ages of Calradia and load the existing campaign.
3. Move the player party within interaction range of the completed refuge.
4. Enter the refuge.
5. Confirm the open tan-green plains from the reference screenshot loads.
6. Confirm the authored fort appears near the calibrated center.
7. Wait at least 20 seconds and confirm the mission does not return automatically to the campaign map.
8. Confirm player movement and Tab exit.
9. Record which props have collision and which remain visual-only.

If the result is wrong, do not change scenes again. First inspect the newest log entries for:

- `Scene=battle_terrain_biome_130`
- `NeedsRandomTerrain=True`
- `RandomTerrainSeed=10840415`
- `Using calibrated open-plains refuge layout anchor`
- `Placed player-authored refuge fort`
- `Refuge scene initialization completed`
- `AccessViolationException`
- `Refuge scene initialization timed out`

The log must establish whether the error is terrain generation, anchor selection, prefab creation, or mission initialization before another change.

## Build and deployment

Build command:

```powershell
dotnet build "D:\AI-Related Apllications & Modding\Modding\Bannerlord Modding Stuff\_TwelveMonthCalendar\TwelveMonthCalendar.csproj" -c Release
```

Never deploy while Bannerlord or a TaleWorlds process is running. Copy the resulting DLL to both:

- Installed module root: `AgesOfCalradia.dll`
- Installed `bin\Win64_Shipping_Client\AgesOfCalradia.dll`

Also synchronize the changed `GUI`, `ModuleData`, `Prefabs`, and `SceneObj` content. Compare SHA-256 hashes after deployment.

## Preserved files

- Editor backups: `EditorBackups`
- Experimental prefab backups: `EditorBackups\ExperimentalPrefabs`
- Authored editor scene: `SceneObj\rct_refuge_fort`
- Builder draft: `C:\Users\fpicc\Documents\Mount and Blade II Bannerlord\Configs\AgesOfCalradia\RefugeLayoutDraft.xml`
- Combined builder exports: `C:\Users\fpicc\Documents\Mount and Blade II Bannerlord\Configs\AgesOfCalradia\CombinedPrefab`

Do not delete, reset, or replace these while stabilizing the runtime fort.

## Planned refuge architecture

The current refuge is an isolated mission and campaign landmark, not yet a genuine settlement. The longer-term plan is an original `RefugeSettlementComponent` with proximity interaction, garrison, AI raids, reduced garrison upkeep, staff, upgrades, stash, and ship storage restricted to river/coast sites.

Do not begin that settlement conversion until the plains mission, authored fort, movement, exit behavior, and stable collision strategy are verified.
