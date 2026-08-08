# Configurability research and original architecture

This audit identifies how to make the mod configurable without creating three
different sources of truth. It does not change runtime behavior or copy code
from another mod.

## Research findings

The project currently has three configuration surfaces:

1. `CalendarSettingsState` is the runtime state and standalone XML persistence
   layer.
2. `CalendarOptionsTabPatch` exposes a native Bannerlord Calendar tab without
   requiring MCM.
3. `McmSettings` is an optional MCM v5 adapter that mirrors values into
   `CalendarSettingsState` and writes the standalone XML.

Useful local references:

- [Runtime state and XML load/save](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarSettingsState.cs:20>)
- [Native Calendar options tab](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarOptionsTabPatch.cs:156>)
- [Optional MCM adapter](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/McmSettings.cs:1>)
- [Save-safe campaign profile](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarSaveCompatibility.cs:40>)

MCM v5 officially supports Global, PerCampaign, and PerSave settings, plus
bool, integer, float, text, dropdown, and button controls. Its documentation
also warns that settings use no persistence provider unless `FormatType` is
overridden; this project already selects JSON in `McmSettings`. See the
[MCM settings overview](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5.html),
[scope and persistence guidance](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5.html#types-of-settings),
and [attribute controls](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5-attributes.html).

## The recommended source-of-truth model

Keep one typed runtime schema and make each front end an adapter:

```text
XML defaults/config      MCM controls      Native Calendar tab
          \                  |                  /
           -> SettingsCommand + validator ->
                      CalendarSettingsState
                              |
             runtime consumers and save profile
```

Every setter should go through one command path that validates the complete
request, applies it atomically, raises one `SettingsChanged` event, and saves
the correct scope. MCM setters should not independently write half-applied
groups while a related value is still being changed.

The native tab and MCM should remain available together, but neither should
own a second copy of gameplay state. XML should remain the no-MCM fallback and
initial default source; MCM should be a UI adapter, not a competing simulation
database.

## Configuration scopes

| Scope | Examples | Persistence | Runtime rule |
| --- | --- | --- | --- |
| Global | 12/24-hour clock, labels, date format, map label mode, UI zoom preference | MCM JSON or standalone XML | Safe to change immediately |
| Campaign profile | month lengths, leap-year policy, campaign time scale, pregnancy model, annual-balance profile | Existing save-safe `CalendarCampaignProfile` | Lock values that change the campaign timeline after start |
| Per-save gameplay | economy thresholds, caravan pressure, camp/refuge costs and durations, optional balance toggles | Save-safe primitive profile or campaign behavior | Restore before dependent behaviors tick |
| Runtime session | diagnostics verbosity, telemetry interval, temporary visual refresh settings | Not saved or global only | Safe to change immediately |

MCM PerSave is technically available, but the official documentation warns
that saving a campaign without MCM can permanently wipe those settings from
the save. Therefore gameplay settings that must survive without MCM should
continue using the mod's own primitive save profile, while MCM edits that
profile through the adapter.

## What is already configurable

The following controls already have runtime state and at least one UI path:

- month and season names;
- month lengths, with campaign-profile locking;
- leap years;
- date format, day/year labels, ordinal suffixes, and 12/24-hour display;
- campaign time scale and automatic pacing;
- fast-forward multiplier, bounded to Bannerlord's supported 1x–4x range;
- pregnancy mode and duration;
- renown and lord-death multipliers;
- annual-balance master switch and subsystem switches;
- annual-balance diagnostics.

These should be migrated to a shared schema and retained during the first
configuration refactor rather than renamed or duplicated.

## Missing controls to expose

### Calendar and clock

- Morning start, default `05:00`;
- Noon start, default `12:00`;
- Noon end, default `14:00`;
- Afternoon end, default `18:00`;
- Evening end, default `21:00`;
- clock refresh threshold during fast-forward;
- clock-synchronized visual lighting with configurable sunrise, sunset, and
  transition hours; native gameplay sunrise/sunset remain untouched
  is added.

The five time boundaries must be validated as an ordered cycle and displayed
as a separate human-readable period. They must never alter native sunrise,
sunset, or `CampaignTime.IsDayTime`.

### Economy and caravans

The newly implemented anti-stagnation values are currently hard-coded and are
the highest-value next controls:

| Setting | Current value | Safe range | Scope |
| --- | ---: | ---: | --- |
| Town low-liquidity threshold | 2,500 gold | 0–25,000 | Per-save gameplay |
| Maximum native recovery blend | 75% | 0–100% | Per-save gameplay |
| Minimum food for liquidity recovery | 25 | 0–500 | Per-save gameplay |
| Caravan low-gold threshold | 2,500 gold | 0–25,000 | Per-save gameplay |
| Caravan low-food threshold | 80 | 0–1,000 | Per-save gameplay |
| Maximum caravan priority bonus | 35% | 0–100% | Per-save gameplay |
| Food pressure contribution cap | 16% | 0–50% | Per-save gameplay |
| Gold pressure contribution cap | 10% | 0–50% | Per-save gameplay |
| Caravan shortage routing enabled | enabled | on/off | Per-save gameplay |

The settings must preserve the current safety rules: no fixed gold injection,
no caravan teleportation, no routing override during siege/starvation, and no
bonus above the configured score cap.

### Camp and refuge

Expose player-facing rules, not raw engine asset IDs:

- “Wait here for some time” duration, default 8 hours;
- minimum rest wait, default 1 hour;
- rest-until-dawn enabled;
- show temporary siege-tent marker;
- minimum party size for refuge construction;
- minimum camp funds;
- construction cost and construction duration;
- guard-tower construction cost/duration;
- interaction radius;
- refuge water-access policy;
- refuge style dropdown using registered authored definitions.

Prefab IDs, scene IDs, collision flags, native scene profile IDs, and physics
creation switches should remain developer-only constants. Exposing those as
free text would turn a safe UI setting into a crash or invalid-scene surface.

### Strategy map and diagnostics

- minimum/maximum map zoom and step;
- marker spacing;
- label mode: all, short names, selected settlement, or hidden;
- label font size and collision padding;
- territory layer, siege badge, and settlement marker toggles;
- refresh interval for the composed atlas;
- telemetry enabled, interval, log level, and sampled settlement count;
- crash-report retention count.

Map viewport dimensions should remain presentation constants unless a proper
layout scale is added; changing them from MCM can break controller navigation
and clipping.

## Validation rules

Use one validator before changing state:

- reject NaN, infinity, empty names, and malformed delimited lists;
- clamp safe numeric values to documented ranges;
- enforce ordered time boundaries and prevent zero-length periods unless
  explicitly supported;
- enforce month lengths totaling 365 common days;
- reject timeline/profile changes after campaign start, or require a new
  campaign/restart;
- enforce dependency rules such as automatic pacing overriding the manual
  campaign scale;
- validate caravan bonus caps against the native score path;
- validate refuge style values against the registered catalog, never arbitrary
  strings;
- report one user-facing error and leave the previous valid state intact.

## MCM implementation strategy

Use `AttributePerCampaignSettings<T>` for settings that should be shared by
all saves of one campaign, and `AttributePerSaveSettings<T>` only for values
that are intentionally MCM-dependent. For this mod, the preferred path is a
thin `CalendarMcmSettings` adapter over the existing campaign profile because
it preserves compatibility when MCM is removed.

Use MCM controls as follows:

- `SettingPropertyBool` for feature switches;
- `SettingPropertyInteger` for days, hours, gold, radii, and counts;
- `SettingPropertyFloatingInteger` for multipliers, probabilities, and score
  bonuses;
- `SettingPropertyDropdown` for clock preset, label mode, and registered refuge
  style;
- `SettingPropertyText` only for names and date formats;
- `SettingPropertyButton` for reset, import/export, and diagnostics actions;
- `IsToggle = true` for group-level enable switches;
- explicit `RequireRestart = true` for timeline changes.

MCM's built-in preset support is useful for “Vanilla-like”, “Calendar”,
“Economy Relief”, and “Hard Economy” presets, but presets must write through
the same validator and save profile. They should not directly mutate private
fields.

## Recommended rollout

1. Create a typed `CalendarConfig` schema with version, scope, defaults, and
   validation; keep `CalendarSettingsState` as the compatibility facade.
2. Move the current hard-coded economy/caravan constants into the schema.
3. Add the clock-period boundaries and map display settings.
4. Add camp/refuge player-facing controls while keeping engine IDs internal.
5. Extend `CalendarCampaignProfile` with versioned economy/refuge fields and
   migrate missing fields to defaults.
6. Make XML, native Options, and MCM call the same `ApplyConfig` transaction.
7. Add presets, import/export, and a read-only effective-settings diagnostic.
8. Test new campaign, existing save, MCM absent, MCM present, mid-campaign
   setting changes, save/reload, reset, and malformed XML/JSON.

The first implementation slice should be economy/caravan thresholds and clock
period boundaries. It is small, visibly useful, and can be tested without
changing the save schema for refuge construction.
