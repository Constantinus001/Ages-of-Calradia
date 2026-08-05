# Realistic Calendar Tweaks - Continuation Handoff

## Repository and live module

- Repository: `D:\AI-Related Apllications & Modding\Modding\Bannerlord Modding Stuff\_TwelveMonthCalendar`
- Git remote: `https://github.com/Constantinus001/Realistic-Calendar-Tweaks.git`
- Current branch: `agent/calendar-settings-audit`
- Current committed HEAD: `29234ff Release v1.5.1 audit fixes`
- Module manifest version in the working tree: `v1.5.2`
- Live Bannerlord module:
  `C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\RealisticCalendarTweaks`

The working tree is intentionally dirty and contains substantial user work. Do
not use `git reset`, `git checkout --`, or `git stash` to clean it. Release
artifacts for v1.5.2 exist locally, but this current strategic-map work has not
been committed, tagged, or released.

## Latest user goal

Finish the **World Calendar -> Strategic Map** so that:

1. every province interior is visibly filled with its current owner's faction
   color;
2. the black province border lines remain visible above the fills;
3. active sieges alone receive a transparent attacker-faction contested overlay;
4. the red static city-name artwork is not displayed;
5. the live town/castle ownership markers stay accurate and selectable; and
6. panning, zooming, Refresh, and Close continue to work.

The user tested the map before the latest XML fix. Their screenshot showed
colored fragments with large white gaps, even though the source masks fully
cover the provinces.

## Latest fix - ready to deploy and user-test

The primary defect was a layout error, not missing art:

- The province and marker layers used `GridWidget`.
- A GridWidget owns its children’s positions and adds an implicit row/cell
  location before their absolute `PositionXOffset` and `PositionYOffset`.
- The prior zero-size-cell workaround was not reliable in Bannerlord’s layout
  engine, so the map could still show white gaps and shifted fragments.

The prefab now uses a plain data-bound `Widget` for both map layers,
which is the pattern used by Bannerlord marker screens. Each province mask is
rendered once at its absolute map coordinate; no layout container changes that
position. The marker layer now contains a small real `ButtonWidget` for every
town and castle. Selecting one marks it in gold and replaces the right panel
with its owner and—when the player owns it or serves its faction—live
prosperity, loyalty, security, militia, and food-stock figures.

The generic base `Widget` is deliberate: in the live game `ImageWidget` did
not resolve the dynamic `@SpriteName` binding, while `Widget` does. The old
red-label layer has been removed. A second matching direct layer is populated
only for live sieges (`Settlement.IsUnderSiege`); it washes the province with
the attacking faction's color at 53% opacity, retaining the normal owner
color and black border underneath. No Modding Kit import is needed for this
C#/XML change.

## Strategic map architecture

### Runtime files

- `WorldCalendarScreen.cs` - ScreenBase/Gauntlet screen lifecycle.
- `CalendarWorldLedgerBehavior.cs` - ledger and map-world refresh hooks.
- `CalendarWorldLedgerVM.cs` - tab UI, calendar grid, zoom/pan state, current
  settlement owners, region colors, and live settlement markers.
- `CalendarStrategicMapLayout.cs` - source-map crop, all 133 province bounds,
  sprite-to-local-settlement bindings, and map coordinates.
- `GUI\Prefabs\WorldCalendar\WorldCalendar.xml` - World Calendar screen and
  Strategic Map canvas.

### Map assets

- `GUI\SpriteParts\ui_world_calendar\strategic_map.png`
  - 1730x1720 base geography with white land and black province borders.
- `GUI\SpriteParts\ui_world_calendar\strategic_city_labels.png`
  - retained as an unused asset; the strategic-map prefab deliberately no
    longer renders it because settlements are represented by clickable markers.
- `GUI\SpriteParts\ui_world_calendar\strategic_province_001.png` through
  `strategic_province_133.png`
  - transparent, alpha-only province-interior masks.
- `GUI\RealisticCalendarTweaksSpriteData.xml`
  - all map sprites are packed in the `ui_world_calendar` category.
- `AssetSources\GauntletUI\ui_world_calendar_1.png` and
  `Assets\GauntletUI\ui_world_calendar_1_tex.tpac`
  - imported sprite atlas source and game resource.

Every one of the 133 source mask files has non-empty, fully opaque alpha
pixels. A local atlas audit also confirmed that every packed atlas region
matches its source mask exactly. Do not recreate the PNGs merely to solve the
previous white gaps; that issue was caused by the grid row offset.

### Layer order in `WorldCalendar.xml`

1. `strategic_map` base image (contains black borders)
2. live-tinted province masks
3. transparent besieger-faction masks for active sieges only
4. live town/castle ownership markers

`ToProvinceColor` in `CalendarWorldLedgerVM.cs` uses an `FF` alpha suffix.
The mask edges are transparent, so fully opaque interiors do **not** cover the
black boundaries in the base map.

`GetBesiegerFaction` follows Bannerlord's current campaign state:

```csharp
settlement.IsUnderSiege
settlement.SiegeEvent.BesiegerCamp.MapFaction
```

The overlay has no entries for a settlement without a `SiegeEvent`, so normal
ownership never looks contested.

The open strategic map also refreshes once per campaign day even if no fief
changes owner. This is required because a siege can begin or end without
raising `OnSettlementOwnerChangedEvent`.

Each settlement marker also exposes `IsUnderSiege`; the map renders the native
`MapIncidents\siege` crossed-weapons emblem above that marker only during the
live siege.

## Current unreleased asset repair

The original province masks covered only 86.56% of bright land pixels, which
caused the visible white gaps. `Tools\Rebuild-StrategicProvinceMasks.ps1` has
rebuilt all 133 mask PNGs from the base map's actual black border components.
The revised masks cover 97.91% of bright land pixels, retaining transparent
edge pixels for the black outlines. The packed atlas must be updated before
deployment, but the stable SpriteData layout means no Modding Kit import is
needed when the patch script below is used.

Correction: do not use SpriteSheetGenerator alone for this atlas. It can
repack the bitmap without updating `RealisticCalendarTweaksSpriteData.xml`,
which makes the game sample wrong source rectangles. Instead run
`Tools\Patch-StrategicMaskAtlas.ps1`; it copies the repaired mask images into
the existing atlas coordinates declared in SpriteData. The strategic coverage
test now verifies every packed pixel, so it catches this exact mismatch.

### Ownership behavior

`BuildStrategicMapLayers()` reads current campaign ownership directly:

```csharp
IFaction currentOwner = settlement.OwnerClan.MapFaction ?? settlement.OwnerClan;
```

The province tint binds to a local settlement registration. The marker layer
has one clickable marker for every campaign town/castle and is the authoritative
precise ownership display. It includes 57 town anchors, 66 observed castle
anchors, and 10 audited fallback castle anchors. Detailed figures are hidden
for other factions unless the player owns the settlement or is a member of its
owner’s faction.

Important limitation: the supplied reference map artwork is not a perfect
one-fief-per-polygon data set. The 133 visual province masks are registered to
their nearest/audited local fief, while the markers track every real fief. Do
not claim an exact province-to-fief simulation for third-party campaigns until
a proper settlement-to-province data set has been created.

## User-facing test checklist

With Bannerlord closed during deployment, launch the game and:

1. Open the campaign map and click the small World Calendar button below the
   map-bar clock.
2. Select **Strategic Map**.
3. Confirm every land region has a solid faction color bounded by black lines;
   no cumulative downward displacement or large white gaps should remain.
4. Click both a town and a castle marker. The selected marker should gain a
   gold border and the right panel should show its name and owner. Owned/faction
   settlements should also show live figures; other factions should show the
   access restriction.
5. Confirm town and castle square markers match their live owners.
6. Press `+`, `-`, and `Reset`; drag the map; then use `Refresh` and `Close`.
7. Capture a screenshot if any individual region still remains white or is
   colored by a distant faction.

If the layout is now correct but a specific province has the wrong *owner*,
edit only its local entry in `CalendarStrategicMapLayout.cs` rather than
changing the owner tracker or marker code.

## Verification commands

Run from the repository root:

```powershell
dotnet build .\TwelveMonthCalendar.csproj -c Release --no-restore
dotnet build .\TwelveMonthCalendar.csproj -c Release --no-restore '-p:OutputPath=bin\Win64_Shipping_wEditor\'
powershell -ExecutionPolicy Bypass -File .\Tests\Verify-StrategicMapCoverage.ps1
powershell -ExecutionPolicy Bypass -File .\Tests\Verify-CalendarMath.ps1
git diff --check
```

`Verify-StrategicMapCoverage.ps1` currently checks:

- 133 campaign towns/castles;
- 133 marker anchors;
- 133 province registrations and masks;
- source/atlas sprite coverage;
- direct live-owner lookup;
- opaque province colors; and
- direct absolute-position data-bound map layers; and
- clickable settlement/castle markers with faction-gated details.

The checks passed immediately before the latest prefab deployment. They cannot
replace the in-game visual test above.

## Safe deployment procedure

1. Confirm Bannerlord and the launcher are closed:

```powershell
Get-Process -Name Bannerlord,Bannerlord.Native,TaleWorlds.MountAndBlade.Launcher -ErrorAction SilentlyContinue
```

2. Copy only files that changed. For the latest fix, this was:

```text
GUI\Prefabs\WorldCalendar\WorldCalendar.xml
```

3. If C# changed, copy both client and editor DLLs:

```text
bin\Win64_Shipping_Client\RealisticCalendarTweaks.dll
bin\Win64_Shipping_wEditor\RealisticCalendarTweaks.dll
```

4. Compare SHA-256 hashes of source and deployed targets.

Only run a Modding Kit sprite import when changing PNG sprite parts or
`RealisticCalendarTweaksSpriteData.xml`. After import, deploy the updated
`Assets\GauntletUI\ui_world_calendar_1_tex.tpac`, sprite data, and assets as
needed. XML/C# edits alone do not need an import.

## General mod state

- Native Calendar settings are interactive, categorized, and include category
  resets. Diagnostics/reset behavior has been audited previously.
- The map bar shows configurable date format, season, and 12/24-hour clock.
- Automatic campaign pacing defaults to `0.23`; its toggle is meant to disable
  when the slider deviates from `0.23` and restore `0.23` when re-enabled.
- The World Calendar has tabs: All Events, By Day, Diplomacy, Settlements,
  People, and Strategic Map.
- The current artifact folder contains `RealisticCalendarTweaks-v1.5.2.zip`,
  but do not treat it as a release candidate until the strategic map visual
  regression is confirmed and the current source has been committed.

## Release warning

`Tests\Verify-Release.ps1` intentionally rejects a dirty source tree. The
next model should not attempt a GitHub release until it has reviewed the full
diff, committed the intended v1.5.2+ changes, passed the release gate from a
clean worktree, and received explicit user direction to publish.
