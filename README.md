# Realistic Calendar Tweaks

See [CHANGELOG.md](CHANGELOG.md) for the complete release history, including v1.5.7.

## Documentation location

The canonical working documentation for this mod is kept outside the runtime
module at:

```text
D:\AI-Related Apllications & Modding\Modding\Bannerlord Modding Stuff\bannerlord modding documentation
```

The canonical folder is the source of truth for development and design guides.
The module keeps this root README only because the release archive requires a
player-facing README.

This Bannerlord v1.4.7 module changes the campaign calendar to a 365-day year
with Gregorian-style month lengths:

```text
Spring April 1st 1084
```

The displayed months are:

- January through December, with real-world seasonal boundaries:
  - Spring: March 21–June 20
  - Summer: June 21–September 20
  - Autumn: September 21–December 20
  - Winter: December 21–March 20

The module uses:

- 365 campaign days per year
- January 31 days
- February 28 days, or 29 days in Gregorian leap years
- March 31 days
- April 30 days
- May 31 days
- June 30 days
- July 31 days
- August 31 days
- September 30 days
- October 31 days
- November 30 days
- December 31 days
- seasons that change on the real-world dates above
- campaign start preserved as April 1, which is in Spring
- campaign time slowed to approximately 0.15x so a normal campaign day takes
  about 8.9 real minutes and the clock is easier to follow
- daily party wages scaled to approximately 0.23x so annual wage pressure is
  comparable to the native 84-day year
- party map speed uses a common base speed of 4.0; Bannerlord's native troop,
  prisoner, herd, terrain, skill, army, and encumbrance modifiers remain active
- pregnancy due dates set to nine configured calendar months after conception
- positive renown rewards reduced to 50% by default to slow clan-tier progression
- native diplomacy kept on its original annual cadence: AI kingdom proposals,
  war/peace cooldowns, treaty durations, alliance durations, clan and mercenary
  tenure, and army/raid/siege influence awards are scaled for the 365-day year

The date is changed by patching `CampaignTime.ToString()`, which is also what
the campaign map time-control UI uses for its visible date. This means dates in
other native messages that rely on `CampaignTime.ToString()` use the same format.

## Build and release verification

Release archives contain the complete single runtime module: module XML, module
data, README, Harmony, the MCM v5 core and calendar adapter, compiled module
DLLs, and UI prefab XML files. The
retired Better Time adapter and the old-module save bridge are intentionally
excluded. Development and verification scripts, logs, debug symbols,
unfinished scene-editor work, editor backups, and shader caches are
intentionally excluded.

Open `TwelveMonthCalendar.csproj` in Visual Studio, or run:

```powershell
& .\Tests\Verify-Release.ps1
```

This command builds the module, creates the complete runtime ZIP in
`artifacts`, verifies its exact file list, and runs a mandatory Microsoft
Defender scan. It also refuses uncommitted sources and rejects an invalid
Bannerlord module-version format. Only upload an archive after it reports
`PASS`.

For the diagnostics-enabled **v1.5.7 Test** archive, run:

```powershell
& .\Tests\Verify-Release.ps1 -IncludeStrategicProvinceDiagnostics
```

This produces `artifacts\RealisticCalendarTweaks-v1.5.7-Test.zip`. It is for
testers and includes strategic-province snapshot diagnostics plus every
module-owned refuge/editor scene. Camps and refuges are enabled only in this
Test archive; their behaviors and map-bar button are disabled in the normal
player archive. Use the normal archive for players.

The build output is written to `bin\Win64_Shipping_Client`. Install the
`RealisticCalendarTweaks` module folder in the game's `Modules` directory, then
enable **Realistic Calendar Tweaks** in the Bannerlord launcher.

This changes the underlying campaign-time interpretation so hero aging, native
four-season calculations, and campaign events created with
`CampaignTime.Years()` follow the 365-day year. Day-based timers remain
day-based. New campaigns can be started normally, and existing vanilla or
older Realistic Calendar Tweaks saves are supported.

## Save compatibility

New saves use a primitive soft profile rather than the calendar's former hard
save-lock marker. Existing saves are not converted by rewriting hero records:
the first load detects whether the raw campaign timestamp uses Bannerlord's
native epoch or the mod's Gregorian epoch. Native saves receive a mapped
calendar epoch and age cutover, preserving the visible 1084 calendar date and
each hero's existing age while applying the 365-day rate to future aging.
The cutover is persisted on the next save, so continuing the same campaign does
not make characters appear as babies. Bannerlord may still show its normal
missing-module warning whenever any campaign mod is disabled.

Like TimeLord, the normal runtime assembly defines no `SaveableTypeDefiner` and
writes no module-owned CLR objects into new saves. Calendar profiles, age
cutovers, and the World Calendar ledger use primitive values. Players may
disable the module, accept Bannerlord's normal
missing-module warning, load the campaign, and save to a new slot to remove the
module from later save metadata.

Removing the module restores Bannerlord's native calendar and pacing
immediately; it does not preserve the mod's Gregorian presentation or annual
balance. Test-build users should retrieve anything kept in a refuge stash
before switching builds or disabling the module. Saves
from the old hard-marker era should first be migrated with v1.4.5; the normal
runtime no longer registers those retired custom save types.

Story quests that use Bannerlord's `CampaignTime.Never` no-deadline sentinel
are left untouched by annual deadline balancing. This prevents quests such as
`Inquire at Ostican`, `Establish your Clan`, and `Villagers in Need` from being
mistakenly timed out immediately after the tutorial is skipped.

Leap years use the Gregorian rule: divisible by 4, except century years unless
they are also divisible by 400. The campaign's starting year, 1084, is treated
as a leap year.

The time multiplier slows the campaign clock, not the number of days in the
calendar. Daily settlement, progression, and finance rates are reduced per
campaign day by the native-84-day to 365-day ratio, preserving their
approximate annual rates. Party map speed remains at native values, and
pregnancy defaults to nine calendar months from the moment conception starts.

Native diplomacy is scaled by the same ratio. This preserves the usual annual
frequency of AI peace, war, alliance, trade, policy, and annexation proposals.
Truces use the configured 100-calendar-day term and tribute agreements use the
configured 235-calendar-day term, while the finance layer balances their daily
payments for the longer year.

## Diagnostics

The module writes diagnostics to:

```text
<Bannerlord>\Modules\RealisticCalendarTweaks\Logs\RealisticCalendarTweaks.log
```

The log records module loading, Harmony patch registration, campaign startup,
the current date/season conversion, and a main-hero age check. It does not log
every campaign tick. It also records the active campaign-time multiplier.
If Bannerlord does not have permission to write inside its installation folder,
the module automatically falls back to
`Documents\Mount and Blade II Bannerlord\Configs\ModLogs`.

## Optional MCM settings

The release includes the MCM v5 core and this mod's settings adapter so the
adapter can load safely. The standalone **Mod Configuration Menu v5** UI remains
optional. When that UI is enabled, it exposes calendar, display, pacing,
lighting, life-cycle, progression, annual-balance, diagnostics, and Strategic
Map controls. Without the standalone MCM UI, the native **Calendar** Options tab
provides the same core controls.

The date format supports `{Month}`, `{Season}`, `{Day}`, `{Year}`,
`{MonthNumber}`, and `{DayOfYear}`. On the map bar, the date and season occupy
the two-line block left of the sundial, while the clock appears to its right.
The preset default is **Month-Day-Year**.

## Standalone in-game settings

MCM provides the in-game settings interface when enabled. When MCM is
unavailable, the mod loads its separate **Calendar** fallback layout and shows
an enabled Calendar tab. MCM and the XML settings file support custom format
tokens: `{Month}`, `{Season}`, `{Day}`, `{Year}`, `{MonthNumber}`, and
`{DayOfYear}`.

Use the native tab's top-right **Reset to Defaults** button to restore calendar
settings. The old duplicate reset row is intentionally not shown.

With MCM active, Bannerlord's untouched native Options layout is used: the
**Calendar** tab is absent and MCM's **Mod Options** tab is authoritative. If
MCM is absent or its adapter cannot initialize, the separate Calendar fallback
layout loads with an enabled Calendar tab. Both paths use the same runtime state
and module-local XML file.

Standalone settings are saved to:

```text
Modules\RealisticCalendarTweaks\RealisticCalendarTweaks.settings.xml
```

The module-local file is the active configuration source. On first launch, the
mod migrates an existing Documents-based settings file into the module folder;
otherwise it uses the shipped defaults. Every calendar setting is kept there,
including the twelve month names, month lengths, and annual-balance toggles.
For example:

```xml
<RealisticCalendarTweaks UseLeapYears="true" ShowDayLabel="false" ShowYearLabel="false"
  UseOrdinalDaySuffixes="true"
  AutoCampaignTimeScale="true" CampaignTimeScale="0.15"
  FastForwardSpeedMultiplier="4"
  ClockSynchronizedLighting="true" VisualSunriseHour="6.25" VisualSunsetHour="18.25"
  VisualLightingTransitionHours="2"
  DateFormat="{Month} {Day} {Year}"
  NativeDaysInYear="84" UseCalendarMonthPregnancy="true"
  PregnancyDurationMonths="9" PregnancyDurationDays="273.75" RenownGainMultiplier="0.5"
  LordDeathRateMultiplier="0.2"
  BalancePartyImpairment="true" BalancePrisonerRecruitment="true"
  BalanceNpcMarriage="true" BalanceMapTracks="true" BalanceQuestDeadlines="true"
  AnnualBalanceDiagnosticsEnabled="true"
  Season1Name="Spring" Season2Name="Summer" Season3Name="Autumn" Season4Name="Winter"
  Month1Name="January" Month1Days="31"
  Month2Name="February" Month2Days="28"
  Month3Name="March" Month3Days="31"
  Month4Name="April" Month4Days="30"
  Month5Name="May" Month5Days="31"
  Month6Name="June" Month6Days="30"
  Month7Name="July" Month7Days="31"
  Month8Name="August" Month8Days="31"
  Month9Name="September" Month9Days="30"
  Month10Name="October" Month10Days="31"
  Month11Name="November" Month11Days="30"
  Month12Name="December" Month12Days="31" />
```

Edit the file while the game is closed and restart the campaign module for
changes to take effect. Month names are limited to 24 characters and month
lengths must be between 1 and 1000 and total exactly 365; leap years add one
day to the configured second month. Set
`UseCalendarMonthPregnancy="false"` to use `PregnancyDurationDays` instead.

The settings are not added to campaign, town, castle, or village menus. They are
available on the separate native **Calendar** tab in the Options screen, and in
the standalone MCM UI when it is enabled.
The date-format selector uses **Month-Day-Year** as the default and also provides
**Day-Month-Year** and **Year-Month-Day**. The season is displayed with the date
block regardless of the selected date order. Changes apply immediately to
display settings; campaign-time pacing changes affect subsequent campaign-time
advancement.

Positive renown rewards are multiplied by `RenownGainMultiplier`, which defaults
to `0.5` and can be changed from the Calendar settings tab, MCM, or XML.

The Strategic Map legend and marker spacing are also configurable without
rebuilding the module. Edit these attributes in the generated
`Modules\RealisticCalendarTweaks\RealisticCalendarTweaks.settings.xml` while
the game is closed, then reopen the game:

```xml
<RealisticCalendarTweaks
  StrategicMapShowLegend="true"
  StrategicMapLegendWidth="250"
  StrategicMapLegendHeight="108"
  StrategicMapLegendMarginTop="6"
  StrategicMapLegendIconSize="30"
  StrategicMapLegendFontSize="13"
  StrategicMapMarkerSpacing="52"
  StrategicMapShowSettlementLabels="true"
  StrategicMapLabelFontSize="12" />
```

The values are range-checked so an accidental edit cannot create an invalid
layout. The same controls are available under MCM's **Strategic Map** group.
The live map atlas is the only custom texture provider. Legend icons are drawn
by dedicated town and castle widgets, so a legend icon cannot be selected or
cached as the map texture, and it cannot be stretched into the map viewport.

Annual Balance, its five scoped toggles, and Lord Death Rate Multiplier are
available in XML, optional MCM, and the native Calendar Options tab. They may
be changed during a campaign and affect future calculations; existing quest
deadlines are never rewritten. Annual Balance is a master switch for finance,
food, settlement, diplomacy, and other annual-rate conversions while leaving
the calendar and display active. Fast-Forward Speed Multiplier may be changed
live from 1x to Bannerlord's supported 4x maximum; normal map pace remains
fixed.

For the 365-day calendar, the balance layer scales identified native daily
economy, settlement, progression, and probability systems by the native-84-day
to 365-day ratio so their approximate annual behavior remains comparable.
Diagnostics record applied patches and compatibility fallbacks. XML, MCM, and
the native Calendar Options tab all use the same persisted settings state.
