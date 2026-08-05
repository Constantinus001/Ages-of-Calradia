# Changelog

All versions target Bannerlord Native v1.4.7.

## Unreleased hardening follow-up

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
