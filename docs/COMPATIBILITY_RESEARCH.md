# Compatibility-first research

The goal is broad compatibility with Bannerlord mods while keeping the
Gregorian calendar available. Absolute compatibility is impossible when two
mods intentionally replace the same native model or method, so the mod should
detect ownership, disable only the conflicting feature group, and keep
additive features running.

This is an original compatibility design. It does not copy code from other
mods or decompiled projects.

## Research basis

Bannerlord modules declare required dependencies in `SubModule.xml`; the
project currently requires only the official campaign modules. It packages the
MCM v5 core and adapter while keeping the standalone MCM UI module optional.
The official documentation describes dependencies as the modules a mod
requires to function, and warns that same-ID XML objects can overwrite one
another according to load order. See the [SubModule documentation](https://docs.bannerlordmodding.com/_xmldocs/submodule.html)
and [module structure guidance](https://docs.bannerlordmodding.com/_intro/folder-structure.html).

Harmony's own documentation distinguishes Prefix/Postfix patches from
Transpilers: Prefix/Postfix methods run around the original method, while a
Transpiler changes the generated IL itself. Transpilers are therefore the most
likely to become incompatible when another mod changes the same method. See
[Harmony patch types](https://harmony.pardeike.net/articles/patching.html)
and [Harmony injected values](https://harmony.pardeike.net/articles/patching-injections.html).

MCM v5 supports Global, PerCampaign, and PerSave settings, but its
documentation warns that PerSave values can be lost if a campaign is saved
without MCM. Gameplay settings should therefore remain in this mod's own
save-safe profile, with MCM acting as an adapter. See the [MCM scope and
persistence documentation](https://mcm.bannerlord.aragas.org/articles/MCMv5/mcmv5.html#types-of-settings).

## Current compatibility risk map

| Area | Current mechanism | Risk | Compatibility policy |
| --- | --- | --- | --- |
| Calendar timeline | CampaignTime, MapTimeTracker, and pacing patches | High; another calendar/time mod may own the same clock | One calendar owner; detect foreign owners and offer Display/Compatibility mode |
| Annual economy | Many Postfix patches on settlement, workshop, finance, food, and probability models | High; double annual scaling can silently distort the economy | Disable this group when a foreign economy owner is detected unless the user explicitly overrides |
| Model wrappers | `campaignStarter.AddModel` wrappers for party speed, food, pregnancy, mortality, patrols, marriage, and tracks | High; a later/earlier mod may already supply a custom model | Install only when the current model is native or one of our wrappers; otherwise leave the foreign model untouched |
| Diplomacy | Several result patches plus Transpilers | High; proposal/war/peace changes overlap diplomacy overhauls | Default off when another diplomacy patch is present; prefer Postfix-only replacements in future |
| Map finance | Transpiler on `MapInfoVM.UpdatePlayerInfo` | High; UI/finance overhauls frequently patch this | Move to a separate optional group and disable on any foreign Transpiler |
| Map bar | `MapBarVM.Initialize`, `MapTimeControlVM.Refresh/Tick`, and `GUI/Prefabs/Map/MapBar.xml` | High for UI overhaul mods | Native map bar by default in Conservative mode; custom calendar map bar only when no conflicting owner is detected |
| Strategic map | Separate World Calendar movie and texture provider | Low to medium; mostly additive | Keep enabled unless a replacement World Calendar screen/provider is detected |
| Camp/refuge | Namespaced behaviors, menus, save keys, and additive map marker | Low; does not replace town/economy models | Keep enabled; use unique IDs and disable only the affected menu/UI component on conflict |
| MCM | Packaged core and manifest-declared adapter, initialized through the optional integration bridge | Low | Never make the standalone MCM UI a hard dependency; keep XML/native options functional |

Local audit references:

- [Patch groups and optional-failure handling](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/MySubModule.cs:201>)
- [Core target fingerprint audit](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarPatchSafetyAudit.cs:260>)
- [Model wrapper installation](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/MySubModule.cs:105>)
- [Optional MCM loading](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/OptionalMcmIntegration.cs:16>)

## Compatibility modes

Add one global setting with four explicit modes:

### Auto / Recommended

Run the conflict scanner at startup and activate safe feature groups only.
Keep the Gregorian calendar core, but disable competing economy, diplomacy,
model, or map-bar groups when ownership is ambiguous. Record the decision in
the log and show it in a read-only Compatibility Report option.

### Conservative

Use the calendar display, names, standalone calendar screen, camp/refuge
features, and diagnostics. Disable direct economy, diplomacy, model-wrapper,
and custom map-bar groups unless the user explicitly enables them.

### Full

Use all configured systems. This is for a deliberately curated load order and
should display a warning that overlapping mods may double-apply behavior.

### Diagnostics only

Do not alter gameplay or UI ownership. Report detected conflicts, native model
types, Harmony owners, and which groups would have been disabled.

The default should be Auto. Compatibility should fail closed for optional
features and fail visibly in the report, never silently change economics.

## Conflict detection design

### Harmony ownership scan

Before installing each optional group, resolve its exact native methods and
inspect Harmony's patch information for those methods. Compare patch owner IDs
against this mod's Harmony ID. Classify conflicts by patch type:

- foreign Transpiler: hard conflict; disable the group;
- foreign Prefix that skips the original: hard conflict for result replacement;
- foreign Prefix/Postfix that only observes or adjusts a different concern:
  soft conflict; allow in Auto but log it;
- no foreign patch: safe to install.

The scan must run per target, not per module name. Mod names are not stable
enough to identify ownership, while the actual patched method is the resource
that can conflict.

### Model ownership scan

Before `AddModel`, inspect the current `GetModel<T>()` result:

- native TaleWorlds model: wrapper may install;
- this mod's wrapper: do not install twice;
- foreign wrapper: skip in Auto/Conservative and report the assembly/type;
- unknown/null model: skip safely and log.

Do not stack two annualized food, speed, pregnancy, mortality, or finance
wrappers unless a specific compatibility adapter proves that the outer wrapper
delegates without repeating the same conversion.

### UI/XML conflict scan

The active `MapBar.xml` is a high-risk replacement surface because XML objects
with the same ID can be overwritten by load order. The long-term compatible
solution is to stop requiring a full native `MapBar.xml` replacement:

1. keep the native map bar as the default;
2. add only namespaced child content through a runtime UI extension when the
   optional UI framework is present;
3. fall back to the separate World Calendar screen when it is not;
4. expose the custom map-bar display as an explicit opt-in compatibility
   setting.

This preserves the gameplay clock even when a UI overhaul owns the map bar.

## Patch implementation rules

- Keep one Harmony ID and unpatch only that ID; never remove another mod's
  patches.
- Prefer Postfix result adjustments that preserve the native method and its
  validation rules.
- Use Prefixes only for argument normalization or narrowly documented skips.
- Replace fragile Transpilers with public model wrappers, Postfixes, or a
  separate additive behavior whenever the same result can be reached safely.
- Give every optional group a `Prepare` gate backed by the compatibility
  controller, so a conflict prevents installation before the patch is applied.
- Keep core calendar target validation separate from optional feature health.
- Never catch a conflict and continue with a partially applied economic group.
- Use unique menu IDs, save keys, XML IDs, texture-provider names, and Harmony
  IDs with the `RealisticCalendarTweaks` namespace.
- Do not make MCM, another economy mod, or a UI framework a hard dependency.

## Configuration required for compatibility

The configuration schema should include:

- Compatibility mode: Auto, Conservative, Full, Diagnostics only;
- Calendar core enabled / display-only mode;
- Annual economy group enabled;
- Diplomacy group enabled;
- Model-wrapper group enabled;
- Map-bar integration enabled;
- Strategic map integration enabled;
- Camp/refuge integration enabled;
- caravan liquidity priority enabled;
- “disable on foreign patch” override per group;
- “show compatibility report at startup” toggle.

The per-group switches must be evaluated before patch registration and model
installation, not only inside Postfix bodies. This avoids paying the conflict
cost every tick and prevents a foreign mod from receiving a partially changed
result.

## Verification matrix

Test each mode with representative mod classes, not only named mods:

1. Native-only campaign: all Auto groups active.
2. Economy overhaul: calendar display remains; annual economy group backs off.
3. Diplomacy overhaul: calendar and economy remain; diplomacy group backs off.
4. Party-speed/food overhaul: foreign models remain authoritative.
5. Map-bar overhaul: native/custom World Calendar fallback remains usable.
6. Standalone MCM UI absent: module-local XML and the native Calendar tab still work.
7. Standalone MCM UI present: the MCM page is active and the native Calendar tab is hidden.
8. Save/reload with MCM removed: campaign profile retains gameplay settings.
9. Unknown Bannerlord method fingerprint: only the affected optional group is
   disabled.
10. Two compatible additive mods: both behaviors and save keys remain active.

## Recommended implementation order

1. Add the compatibility mode and per-group settings to the shared config
   schema.
2. Add Harmony-owner and model-owner reporting without changing behavior.
3. Gate economy, diplomacy, map finance, and model-wrapper groups in Auto mode.
4. Replace or opt-in the full MapBar XML override.
5. Replace the two existing Transpiler groups where practical.
6. Add compatibility presets and a visible effective-settings report.
7. Test with clean saves and representative mod categories before deployment.

This gives the mod a safe default for broad load orders while still allowing a
curated Full mode for users who want every calendar system active.
