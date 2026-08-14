# Ages of Calradia - Political and Strategic Map Handoff

## User-approved best baseline

The visual result accepted by the user on 2026-08-10 is preserved at
`backups\Deploy-20260810-195835-BEST-1352-AllIslandExclusions` and packaged as the
matching `.zip`. This baseline supersedes the older restore recommendation in
the "Latest deployed build" section below. See `BEST_BUILD.md` and the package's
`BUILD_MANIFEST.md` for required files and verified hashes.

Updated: 2026-08-10 21:10 America/New_York

## Immediate objective

Runtime-verify and finish the original campaign political-map presentation. At distant zoom it must show dark translucent faction-colour territory over land, mountains, and rivers; preserve seas and coastlines; omit campaign province contours; show one gold kingdom name per realm; hide town names; and keep storms, smoke, and weather above the political layer.

Do not make another visual change from screenshots alone. The deployed build emits topology and frontier diagnostics; inspect the newest runtime log and classify the remaining defect before changing geometry, terrain classification, materials, or render depth.

## Non-negotiable requirements

- This is a clean-room implementation. Do not copy third-party source, IL, assets, textures, meshes, shaders, or implementation constants.
- Research and analyze before implementation. Record evidence for every rejected and accepted approach.
- Campaign province contours are intentionally disabled; the Strategic Map's authored province artwork remains unchanged.
- Political frontiers are opaque, contrasting faction-colour outlines sampled from live faction ownership. They must follow the same land/sea classification as the fill and must never cross open water as straight Voronoi chords.
- Country fill must follow coastlines and must not enter open ocean.
- Rivers and mountain terrain inside owned territory must receive faction colour.
- Storms, smoke, weather, and particles must remain visible above the map tint. Native road, river, and terrain-detail decals must not bleed through the clean political fill or substitute for province borders.
- Avoid long loading stalls, flashing during rebuilds, giant rectangles, and partial visible batches.
- Build, audit, back up, deploy to the installed module, and verify SHA-256 after every accepted fix.

## Project locations

- Source: `D:\AI-Related Apllications & Modding\Modding\Bannerlord Modding Stuff\Ages Of Calradia`
- Installed module: `C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Ages Of Calradia`
- Runtime log: `C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Ages Of Calradia\Logs\AgesOfCalradia.log`
- Main project: `TwelveMonthCalendar.csproj`
- Release assembly: `bin\Win64_Shipping_Client\AgesOfCalradia.dll`
- Research record: `docs\STRATEGY_MAP_IMPROVEMENT_RESEARCH.md`

The worktree contains many intentional user changes and untracked implementation files. Preserve them and do not reset, clean, or overwrite unrelated work.

## Latest deployed build

The verified Release build was deployed while Bannerlord and the TaleWorlds launcher were closed.

- Build: 0 warnings, 0 errors
- Deployed main DLL SHA-256: `2DFE662BC491C69E5E294742FF9D6AE5E11A70EFF252A56440FE458EC4508460`
- Deployed PDB SHA-256: `DEE712B71F516F432EADDB3038A2D4E902019EC2CF95B13DC9DB2795B65C53B5`
- Political label XML SHA-256: `669C1FD1478393F1FE1BB62EA95EE54716121C07095C483E33CE28008E347DA9`
- Strategic-map UI XML SHA-256: `0E457BA761FA9E24469A64CEC0BB27DF25FE25A5EB687D763C1915959725DE21`
- Rollback backup: `backups\Deploy-20260810-211000-highres-native-topology`
- Installed feature audit passed against the deployed DLL.
- Correct mod assemblies are `AgesOfCalradia.dll` and `AgesOfCalradia.MCM.dll`.
- `0Harmony.dll` and `MCMv5.dll` are expected runtime dependencies, not stale mod DLLs.

This build has not yet received a fresh in-game visual verdict. Do not claim the river, coastline, or storm-depth result is fixed until a new screenshot and log confirm it.

## Next runtime evidence to collect

The pending diagnostic build adds one map-preparation summary with these fields. The native water pass now uses a separate 384-column (four-times-dense) grid, built under the same time budget, so these topology figures are no longer limited by the 96-column fill grid:

- Native-water topology: `interiorNativeWaterCells`, `exteriorNativeWaterCells`, `interiorNativeWaterComponents`, `largestInteriorNativeWaterComponent`, plus enclosed `Lake`, `CoastalSea`, and `OpenSea` cell counts.
- Exact classification use: `interiorNativeWaterFillAccepts` and `interiorNativeWaterFrontierAccepts`.
- Frontier construction: `frontierCandidates`, `frontierUnsupported`, `frontierProjectionRejected`, `frontierSaddleCells`, and `frontierAmbiguousCells`.

Use these numbers with a screenshot of Battania's lake and the western islands. In particular, a zero enclosed-water-component count means the coarse native grid itself sees the lake as ocean-connected; a nonzero count with zero fill accepts means the fill did not sample the detected component; and high unsupported/frontier rejection counts identify chord candidates being deliberately suppressed over water.

## Clean-room Kingdom Frontiers research

Inspection was limited to packaging, configuration, public type/member metadata, and observed behavior. No method bodies, IL implementation, source, art, texture, mesh, or material asset was copied.

Observed facts:

- The third-party module contains a small DLL and UI prefab but no custom political-map texture or material asset.
- Its user configuration exposes a coarse grid resolution and opacity.
- Public metadata suggests a grid snapshot, territory construction, cell clipping, terrain-height sampling, and batched fill entities.

Reasonable high-level inference: it builds terrain-relative coloured geometry from a coarse ownership grid and clips/recover cells near land boundaries. That general rendering category is not proprietary code and was used only to validate feasibility.

Our implementation is independently designed:

- 96-column campaign terrain cache.
- Two levels of adaptive refinement only across ownership or land/coast transitions.
- Live settlement Voronoi ownership through a balanced nearest-site index.
- Module-owned `strategic_province_index.png` used as the authored land/coast and province-ID reference.
- Settlement-calibrated authored-map-to-campaign projection.
- Time-sliced cache and mesh construction while campaign time is paused.
- Connected marching-grid province contour extraction.
- Original labels, friendly-army markers, player/clan-party markers, legend, and selection glow.

## Current political-map architecture

### Territory fill

`CampaignPoliticalTerritoryFill.cs`

- Opaque `vertex_color_mat` meshes; the rejected blend material visibly preserved roads, rivers, and terrain detail.
- Political fill colours preserve the original live faction hue, scale every RGB channel uniformly to 50%, and reach full opacity at fully distant zoom. This matches the clean flat-colour reference while making it darker and preventing terrain washout. Country borders remain fully opaque.
- One bounded row entity per batch; maximum eight rows and four milliseconds per map frame.
- Double-sided triangles avoid undocumented culling assumptions.
- Pending entities remain hidden and swap atomically when complete.
- Terrain-relative height: `FillHeight = 4f`; cells with at least `0.75f` of center-height interpolation error refine up to the existing two-level limit and use exact native terrain heights for their added vertices.
- Fill and river-cap render order: `100`; country frontiers render at `108`.
- Terrain-detail decals are not forced over the political fill.
- Live faction frontiers use a separate four-times-resolution marching grid. Faction identity suppresses boundaries between settlements owned by the same realm; land/sea transitions produce coast-following outlines. The single opaque ribbon is `1.6f` wide and split lengthwise at its center: each half uses 85% of the live faction RGB brightness sampled `2.75f` into that side. This remains brighter than the 50% territory fill for compensating contrast. Coast segments reuse the coastal faction colour on both halves. Each segment has four-step terrain-draped semicircle joins, but a quantized endpoint set emits each shared cap only once so coplanar caps cannot flash. Every outer, center, and cap vertex receives an exact terrain height. Render order is `108`.
- Alternating land/water saddle cells use center-classified marching-squares pairing instead of four spokes through the cell center. Every resulting segment is also sampled at five positions and rejected if neither side has land support. This prevents straight frontier chords between islands or across open-water channels while preserving coast-following outlines.
- Strict authored land is now distinguished from narrow-gap recovery. Any recovered gap receives an exact native-terrain check and must sit above the measured open-sea ceiling unless native terrain identifies it as land or river. Nearby islands therefore retain separate coastline loops instead of being combined by the inland-river recovery heuristic.
- Frontier topology is stricter than fill recovery: outside strict authored pixels, native generic `Water` is always water for borders, while native `River` remains land. This prevents archipelago channels from joining nearby islands into a single frontier without reopening visible river holes in the political fill.
- Native `Lake` is political interior terrain for both fill and ownership topology, so Battania's enclosed lake receives Battania's colour instead of becoming a hole or an internal country coastline. Sea terrain and generic-water island channels remain excluded.
- Authored water topology is also flood-filled from the atlas edge once at load. Only unvisited water holes are enclosed lakes; they are filled and treated as interior by both fill and frontier logic. Edge-connected water stays sea, so the archipelago remains separated even where native terrain calls it generic `Water`.
- Before that flood fill, only four-pixel opposing-land separator gaps are sealed. This removes the index atlas's thin black province-divider network from water connectivity without bridging the materially wider channels between islands.
- The final authority is now a second connected-component pass over the already sampled native terrain grid: all water connected to a grid edge remains sea, while enclosed native-water components become political interior. This corrects the Battania lake even if the strategic atlas's divider network makes it appear edge-connected.
- Runtime evidence showed the Battania lake arrives as native `CoastalSea` rather than `Lake` or generic `Water` (`nativeWaterCells=0`, `nativeLakeCells=0`). Enclosed native-water topology now takes precedence over that raw terrain label; only edge-connected `CoastalSea` remains excluded.
- Fill completion is no longer held behind frontier completion. The 162 fill entities are atomically published and political labels rebuilt as soon as fill rows finish; the previous active frontier remains until its separately built replacement is complete. This prevents both the political map and borders disappearing during expensive frontier preparation.
- Fill and frontier entities are stored separately after their atomic build. Faction fill retains the distant political-zoom fade; two-sided faction frontiers remain fully opaque at normal/close campaign zoom, leaving native terrain unfilled beneath them. Their entity frame lowers by `4.65f` at close zoom, changing the terrain-relative height from political `+5f` to z-fight-safe `+0.35f`, and smoothly returns to `+5f` with the political fade.

The former `+12` fill height was rejected because faction-coloured geometry depth-cut through smoke even with an early render order.

### Terrain and river classification

`CampaignMapTerrainGridCache.cs` and `CampaignStrategicLandMask.cs`

Classification precedence:

1. Authored strategic-map land is accepted unless native terrain is protected water.
2. `CoastalSea`, `OpenSea`, and `SeaRestriction` are always protected and remain unfilled. Native `Lake` receives the nearest kingdom fill.
3. Native land and native `River` are accepted.
4. Generic native `Water` is not blindly accepted. It uses the elevation-aware recovery path.
5. Water at or below the measured open-sea ceiling is rejected.
6. Interior water at least `2.5f` above that ceiling is accepted.
7. Lower elevated channels require opposing authored land banks within the extended channel search.

Native v1.4.7 inspection confirmed that `CampaignVec2.IsValid()` combines face existence with party-navigation eligibility. Terrain probes now use `CampaignVec2.Face.IsValid()` so valid `Water` and `Lake` faces are not discarded before classification. Fine authored-mask samples keep the fast path except inside coarse protected-water cells, where an exact native probe prevents adaptive coastline slivers without restoring the rejected all-point navigation scan.

The post-deployment classifier run still reported 81 native `River` cells while rivers remained visibly uncovered. A native-water-height experiment sampled 2,710 river triangles but lifted none, proving the existing geometry was already above the reported water surface. That depth approach was removed. River-classified triangles now use a separate opaque cap mesh at render order `100`, and those cap entities do not force river decals above themselves. Normal land also renders at `100`; faction frontiers render at `108`. Campaign province contours are disabled.

The sea ceiling uses perimeter-water median and median absolute deviation instead of a fixed world height. The next runtime log reports native river/water/lake/coastal/open-sea counts so the classifier can be adjusted from evidence.

### Province contours

Campaign province contours are disabled by removing their behavior from startup and the main assembly. The source is retained only as implementation history. This eliminates contour extraction, 114 render entities, and per-frame zoom presentation work. The separate Strategic Map province-border sprite remains enabled.

### Zoom, labels, and weather ordering

`CampaignPoliticalOverlayView.cs` and `GUI\Prefabs\Map\PoliticalKingdomLabel.xml`

- Political fade begins at camera altitude 580 and completes at 740.
- Native settlement names are hidden above altitude 650 and restored below it.
- Kingdom names use live holdings, world-to-screen projection, and collision filtering.
- Labels are standalone gold lettering with no black bars or duplicate shadow text.
- Fill and contour render orders are intentionally early, with close terrain draping, so normal storm/particle/decal visuals should remain above them.

## Strategic-map features already implemented

- Gold country names move when live settlement ownership changes.
- Settlement selection draws a four-sided glow.
- Active player party, player-clan parties, and friendly/allied army leaders have live markers.
- Marker projection uses the calibrated campaign-to-strategic-map transform.
- The legend identifies `P Player party`, `C Clan party`, and `A Friendly army`.
- Friendly-party positions refresh once per second without rebuilding the province texture.
- Country names use one text layer to prevent doubled lettering.

Relevant files:

- `CalendarStrategicKingdomLabels.cs`
- `CalendarStrategicFriendlyArmies.cs`
- `CalendarWorldLedgerVM.cs`
- `WorldCalendarScreen.cs`
- `GUI\Prefabs\WorldCalendar\WorldCalendar.xml`

## Rejected approaches and why

- Giant exact/refined mesh finalized in one operation: caused loading stalls or a stuck map thread.
- Per-navigation-face probing: measured roughly 306,932 probes and a 53.3-second main-thread stall.
- Blindly accepting all generic `Water`: risks flooding coastal bays and open sea.
- Opposing-bank recovery alone with a wide radius: leaked faction colour into coastlines.
- High mesh offsets (`+12` and `+14.4`): cut through storm smoke by depth even with render order changes.
- Independent short tangent strokes: produced random black dashes rather than connected provinces.
- Separate translucent political-frontier ribbons: produced flashing/translucent bands.
- Hiding entities with `SetReadyToRender(false)`: was not reliably reversible; use `SetVisibilityExcludeParents`.
- Unconditional daily rebuilding: unnecessary cost; ownership signatures now gate rebuilds.

## Last known runtime evidence before this build

The preceding build logged approximately:

- Province segments: 5,452
- Province triangles: 41,636
- Political land samples: 55,820
- Sea samples: 19,414
- Political triangles: 55,820
- Refined cells: 9,339
- Political fill entities: 84
- Terrain cache wall time: 256 ms
- Political mesh construction: 3,287 ms spread across bounded frames
- Maximum mesh batch: 111 ms
- Open-sea height ceiling: 0.113
- Base land cells: 3,184
- Elevated recovery cells: 376
- Retained water cells: 6,040

The previous generic-water experiment changed only 12 cells, proving that blind generic-water acceptance did not explain all visible river holes. The last pre-fix runtime log reported `nativeRiverCells=81`, `nativeWaterCells=0`, `nativeLakeCells=0`, `nativeCoastalSeaCells=968`, and `nativeOpenSeaCells=1436`. The zero generic-water/lake counts are now understood to have been filtered through movement validity. The deployed build adds exact-probe and protected-water rejection counters; do not tune terrain thresholds until its new runtime evidence is captured.

## Immediate runtime test

1. Confirm Bannerlord and all exact TaleWorlds launcher processes are closed before starting a new test.
2. Launch only Ages of Calradia's political implementation; disable other mods' overlapping political-map features to prevent duplicate layers.
3. Load a campaign and zoom from normal view to full political view.
4. Record total load time and whether map interaction remains responsive while rows appear.
5. Confirm faction colour covers mountains and inland rivers.
6. Confirm open seas, lakes, and coastlines remain unfilled.
7. Confirm no giant rectangles, strips, random polygons, flashing, or translucent frontier lines appear at close zoom.
8. Confirm no campaign province contours appear at close or political zoom.
9. Confirm storms and smoke render entirely above the faction fill and country frontiers.
10. Confirm town names disappear at distant zoom and restore when zooming in.
11. Capture one full-map screenshot and one close screenshot of a river/coast/storm intersection.
12. Copy the newest political-map diagnostic log entry before changing code.

Search the log for:

- `Political map built`
- `nativeRiverCells=`
- `nativeWaterCells=`
- `nativeLakeCells=`
- `nativeCoastalSeaCells=`
- `nativeOpenSeaCells=`
- `exactNativeTerrainProbes=`
- `exactProtectedWaterRejections=`
- `riverTriangles=`
- `riverEntities=`
- `elevatedRecoveryCells=`
- `retainedWaterCells=`
- `openSeaHeightCeiling=`
- `meshMilliseconds=`
- `maximumBatchMilliseconds=`

Interpretation:

- River hole with native `River`: inspect whether the protected-water precedence or fine-sample path rejected it.
- River hole with generic `Water` and high terrain: inspect projection/elevation sampling; do not widen coast recovery globally.
- Fill in open ocean: identify the reported native terrain type and sampled height before changing thresholds.
- Colour visible through smoke: lower physical mesh offsets further or identify the material/depth behavior; render order alone is already proven insufficient.
- Missing province segment: determine whether ID recovery, contour extraction, land clearance, or coast clipping removed it.

## Build and verification

Run from the source root:

```powershell
dotnet build TwelveMonthCalendar.csproj -c Release
& .\Tests\Verify-MapBorderFeature.ps1 -AssemblyPath .\bin\Win64_Shipping_Client\AgesOfCalradia.dll
& .\Tests\Verify-CalendarMath.ps1
& .\Modules\AgesOfCalradiaRefuges\Tests\Verify-RefugeModule.ps1
git diff --check
```

The current build passes all four checks.

## Safe deployment procedure

Never deploy while any exact process is running:

- `Bannerlord`
- `Bannerlord.Native`
- `TaleWorlds.MountAndBlade.Launcher`
- `TaleWorlds.MountAndBlade.Launcher.Singleplayer`

Before copying:

1. Verify exact process names are absent.
2. Create a timestamped backup under source `backups\Deploy-<timestamp>-<description>`.
3. Back up the installed DLL and PDB plus every changed XML being deployed.
4. Copy the verified Release DLL/PDB and changed UI files into the installed module.
5. Compare source and installed SHA-256 for every copied file.
6. Run `Verify-MapBorderFeature.ps1` against the installed DLL.
7. Audit for stale own-mod names, but retain expected `0Harmony.dll` and `MCMv5.dll` dependencies.

Do not use destructive git cleanup or overwrite unrelated user changes.

## Code-quality status

- Release build: clean, zero warnings and errors.
- Map feature audit: passed against source build and installed DLL.
- Calendar and standalone-refuge regression tests: passed.
- Whitespace/error audit: passed.
- Terrain sampling and mesh construction are bounded by frame budgets.
- Expensive map rebuilds are ownership-signature gated.
- Pending entity sets are atomically swapped.
- Diagnostic counters are available for the next evidence-based iteration.
- Remaining uncertainty is runtime visual behavior, not a known compile/test failure.

## Preserve earlier work

The previous refuge-system handoff was replaced because the political-map work is now the immediate task. Refuge research remains in:

- `Modules\AgesOfCalradiaRefuges\docs\REFUGE_IMPROVEMENT_RESEARCH.md`
- `Modules\AgesOfCalradiaRefuges\docs\REFUGE_FORT_STYLE_FRAMEWORK.md`
- `Modules\AgesOfCalradiaRefuges\SceneObj\rct_refuge_collision_navmesh_workshop\README.md`

Do not delete the separate refuge-module assets, editor backups, strategic-map assets, or unrelated worktree changes while continuing this task.
