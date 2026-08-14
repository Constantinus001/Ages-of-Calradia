# Changelog

All versions target Bannerlord Native v1.4.7.

## Unreleased

- Released Ages of Calradia Refuges v0.1.1 with the restored nine-scene
  climate and water-access matrix, original Native terrain foundations, and
  save repair for refuges rewritten by the temporary single-scene build.
- Released v1.5.10 with the separately loadable Refuges and Logistics source
  modules included in the repository.
- Extracted camps, refuge persistence, missions, UI, prefabs, and scenes into
  the optional `AgesOfCalradiaRefuges` module. Existing refuge save keys are
  retained; load Refuges after Ages of Calradia to continue using a refuge.
- Restored Bannerlord's native tournament scheduling. The calendar no longer
  annualizes tournament start or resolution probabilities, preventing extended
  periods with no newly created tournaments.

## v1.5.9 - Complete identity migration and campaign borders

- Completed the internal migration to the `AgesOfCalradia` module ID,
  installation folder, assemblies, MCM adapter, settings file, diagnostics,
  save keys, and release archive layout.
- Fixed the blank Strategic Map caused by the deployed World Calendar prefab
  requesting the retired texture-provider name after the assembly rename.
- Added live kingdom borders to the campaign map using settlement Voronoi
  cells projected onto terrain and refreshed after ownership changes.
- Improved Strategic Map readability with visible province borders, enforced
  marker separation, and a selection glow around the active settlement.
- Updated release verification for the new module identity, map provider,
  kingdom-border feature, and packaged runtime file list.

## v1.5.8 - Ages of Calradia rename

- Renamed all public-facing launcher, MCM, diagnostics, documentation, and
  release-archive branding to **Ages of Calradia**.
- Retained the legacy `RealisticCalendarTweaks` module ID, installation folder,
  assemblies, settings path, and save keys so existing saves and upgrades keep
  working under the new name.

## v1.5.7 - Existing-save and story-quest compatibility

- Fixed story quests that use `CampaignTime.Never` being timed out by annual
  deadline balancing, including `Inquire at Ostican`, `Establish your Clan`,
  and `Villagers in Need`.
- Added native-save age compatibility. Existing heroes keep their age when a
  campaign is continued, while future aging uses the 365-day calendar rate.
- Applied that cutover to Bannerlord's general elapsed-year getter as well as
  `Hero.Age`, covering simulation code that reads historical year spans.
- Matched TimeLord's removable-save principle: the normal runtime now defines
  no custom save types, and release verification rejects `SaveableTypeDefiner`
  or `SaveableField` usage in the campaign-profile pipeline.
- Disabled camps and refuges in the normal player build. Their campaign
  behaviors, map-click patch, and map-bar button remain enabled in the
  diagnostics Test build only.
- Audited the save-age migration against Bannerlord's `Hero.Age` path: dead
  heroes now use their death day as the compatibility reference, life/death-
  disabled campaigns retain Bannerlord's fixed ages, and compatibility state
  resets safely when another campaign starts in the same process.
- Replaced profile-schema guessing with raw campaign-time basis detection.
  Native saves now map their original epoch to April 1084, while campaigns
  created under an older Gregorian build retain their existing epoch. World
  Calendar events and quest deadlines use the same mapped day basis.
- Kept elapsed durations separate from absolute-date epoch conversion so age,
  season, and year spans cannot inherit the native-save calendar offset.

## v1.5.6 - Calendar navigation and strategic-map refinement

- Added an interactive month calendar with previous/next navigation, event summaries, and quest-deadline markers.
- Added strategic-map settlement tracking, selected-settlement village details, and configurable legend, marker, and label presentation.
- Refined strategic-map zoom, panning, custom atlas rendering, and town/castle legend artwork.
- Improved campaign-map visual-clock, weather, atmosphere, and colour-grade synchronization diagnostics.
- Completed the MCM/native Calendar Options fallback split so each settings surface loads only when appropriate.
- Packaged the optional MCM v5 core and adapter, with JSON settings registration
  so the MCM page is discovered and persists its configuration correctly.

## v1.5.5 - GitHub-ready refuge and strategic-map release

- Added the player refuge workflow: surveyed construction sites, persistent camp anchors, refuge staff and upgrades, the builder HUD, and data-driven fort and scene profiles.
- Added the full World Calendar strategic-map presentation, live settlement markers, siege information, province rendering, and caravan trade-priority coverage.
- Included the complete runtime module data and configured the release archive to include only finished module-owned refuge scenes.
- Removed temporary strategic-province diagnostics from the runtime module and excluded local editor backups and shader caches from source control and releases.
- Restored release documentation, corrected the strategic-map verification provider name, and strengthened release packaging validation.
- Added a separate `v1.5.5-Test` archive that enables strategic-province snapshot diagnostics for tester builds only.

## v1.5.3 and v1.5.4

- No standalone public releases were published under these version numbers.

## v1.5.2 - Camps, refuges, and World Calendar strategic map

- Added portable camps and player refuges, including persistent camp anchors,
  construction-site surveying, staff roles, upgrades, and mission support.
- Added the World Calendar strategic map with live settlement ownership,
  province overlays, town/castle markers, siege information, and caravan
  trade-priority coverage.
- Added the strategic-map artwork, sprite data, source maps, and coverage tests
  needed to render the new World Calendar view.

## v1.5.1 - Settings and campaign-profile hardening

- Added a save-persisted Annual Balance master switch that does not disable the calendar itself.
- Migrates v1.5 schema-3 profiles with obsolete high fast-forward values safely to the supported 4x maximum.
- Made standalone settings writes atomic and remove temporary files after a failed write.

## v1.5.0 - Calendar settings and map-bar update

- Added a fully interactive native Calendar settings page with categories,
  slider arrow controls, per-category resets, and an Annual Balance master toggle.
- Fixed Calendar action rows so editors and reset buttons execute their own
  callbacks after UI refreshes instead of invoking Bannerlord's Benchmark action.
- Added configurable 12/24-hour map time, moved the season label next to the
  time controls, and refined the calendar date and clock placement.
- Made automatic campaign pacing default to 0.23 and restore that state when
  the campaign scale is returned to 0.23.
- Removed fixed pregnancy-duration-days from the settings UI.

## v1.4.6 - Single-module package

- Removed the `_TwelveMonthCalendar` legacy save bridge from the release ZIP
  and live module layout.
- The release now installs only `RealisticCalendarTweaks` and contains no old
  module manifest or old-module DLL.
- New saves continue to use the primitive soft campaign profile introduced in
  v1.4.5. Players with older calendar saves should retain v1.4.5 for any needed
  one-time migration before switching to this single-module release.

## v1.4.5 - Realistic Calendar Tweaks

### Module, saves, and settings

- Renamed the module and its internal ID to **Realistic Calendar Tweaks**.
- Added a lightweight `_TwelveMonthCalendar` legacy bridge so existing calendar
  saves can be migrated without keeping the old runtime DLL active.
- Replaced the explicit save-compatibility marker with a primitive soft campaign
  profile. New saves no longer receive the calendar's hard module-lock marker.
- Migrates standalone XML settings and MCM values into the new
  `RealisticCalendarTweaks` settings identity.

### Campaign controls and life cycle

- Normal map pace is fixed at the Gregorian base cadence.
- Added one live-safe **Fast-Forward Speed Multiplier** setting, using
  Bannerlord's native `Campaign.SpeedUpMultiplier`: 1x-128x, default 4x.
- Added a configurable **Lord Death Rate Multiplier**, defaulting to 0.20.
  It scales ordinary noble-lord old-age and battle mortality while leaving
  executions and scripted deaths unchanged.
- Added the mortality and fast-forward controls to the native Calendar tab,
  optional MCM page, and standalone XML settings file.

### Packaging and diagnostics

- Removed the retired Better Time adapter DLL and its UI dependency.
- Renamed diagnostics and crash-report files to `RealisticCalendarTweaks`.
- Release verification now asserts the renamed module, legacy bridge, direct
  fast-forward implementation, mortality wrappers, and an adapter-free ZIP.

## v1.3 — Production and balance update

### Calendar and interface

- Added the full Gregorian 365-day calendar with twelve named months and
  Gregorian leap-year February handling.
- Added real-world seasonal boundaries and a map-bar display showing season
  and date.
- Added selectable date formats: Day-Month-Year, Month-Day-Year, and
  Year-Month-Day.
- Expanded the map-bar layout for long month and season names.
- Added an in-game calendar settings page when MCM is not installed; MCM is
  optional and takes precedence when available.
- Added file-based configuration for display preferences and month names.

### Pacing and campaign balance

- Slowed campaign-time progression so a 365-day year remains close to the
  native 84-day year in real play time.
- Scaled daily wages and settlement economy rates to preserve annual pressure.
- Added 365-day-year balance coverage for food, prosperity, production,
  character progression, party and garrison training, healing, notable power,
  crime, clan influence, army cohesion, volunteer replenishment, minor-faction
  spawning, diplomacy, and other daily probability systems.
- Set a common map-speed base of 4.0 while retaining native troop, prisoner,
  herd, terrain, skill, army, and encumbrance modifiers for both player and AI
  parties.
- Set pregnancy timing to nine calendar months from conception.
- Added guarded annual scaling for party impairment, prisoner recruitment,
  NPC marriage chance, map tracks, and quest deadlines.

### Stability and diagnostics

- Reworked finance-model integration to avoid startup and character-creation
  crashes caused by unsafe access to native finance initialization.
- Added safe compatibility wrappers for party speed, impairment, prisoner
  recruitment, marriage, map tracks, and finance calculations.
- Added crash flight-recorder logging and opt-in annual-balance diagnostics.
- Added save compatibility markers so v1.3 calendar saves require the module
  when loaded, preventing silent reversion to the native calendar.
- Balance settings that change campaign timing are applied at campaign start to
  avoid unsafe hot-swapping during an active save.

### Release quality

- Added a deterministic release manifest containing every required runtime
  file: module XML, README, Harmony dependency, both module DLLs, and both UI
  prefab XML files.
- Added an automated release gate that builds, verifies the exact archive
  contents, runs Microsoft Defender against the final ZIP, and prints its
  SHA-256 before upload.

## v1.0 — Initial release

- Introduced the Twelve Month Calendar module for Bannerlord.
- Replaced the visible native 84-day calendar with named Gregorian months.
- Added 365-day calendar math, seasonal formatting, map-bar UI integration,
  optional MCM integration, native settings fallback, XML configuration, and
  initial campaign pacing/economy/life-cycle scaling.
