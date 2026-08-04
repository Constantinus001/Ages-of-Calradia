# Realistic Calendar Tweaks

See [CHANGELOG.md](CHANGELOG.md) for the complete v1.0-to-v1.5.0 patch notes.

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
- campaign time slowed to approximately 0.23x so a 365-day year takes about
  the same real play time as Bannerlord's native 84-day year
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
strings, README, Harmony dependency, compiled module DLLs, and UI prefab XML files. The retired
Better Time adapter and the old-module save bridge are intentionally excluded.
Development and verification scripts, logs, debug symbols, and backups are
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

The build output is written to `bin\Win64_Shipping_Client`. Install the
`RealisticCalendarTweaks` module folder in the game's `Modules` directory, then
enable **Realistic Calendar Tweaks** in the Bannerlord launcher.

This changes the underlying campaign-time interpretation so hero aging, native
four-season calculations, and campaign events created with
`CampaignTime.Years()` follow the 365-day year. Day-based timers remain
day-based. Start a new campaign with the module enabled.

## Save compatibility

New saves use a primitive soft profile rather than the calendar's former hard
save-lock marker. This release intentionally does **not** ship the old
`_TwelveMonthCalendar` module or a legacy save bridge. Keep v1.4.5 if an older
calendar save still needs its one-time migration path. Bannerlord may still show
its normal missing-module warning whenever any campaign mod is disabled.

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

MCM is optional. If MCM is installed, the calendar exposes an in-game settings
page with calendar, display, balance, lord-mortality, and fast-forward-speed
controls. The date format supports `{Month}`, `{Season}`, `{Day}`,
`{Year}`, `{MonthNumber}`, and `{DayOfYear}`. Without MCM, the native
**Calendar** Options tab provides the same core controls. On the map bar, the
date appears left of the clock and the current season appears separately to its
right (for example `April 3rd 1084` and `Spring`). The preset default is
**Month-Day-Year**.

## Standalone in-game settings

MCM is not required. The module adds leap-year, label, pacing,
and preset-format controls to a separate **Calendar** tab in Bannerlord's native
**Options** screen. Open the game's Options screen and select the Calendar tab.
MCM and the XML settings file also support custom format
tokens: `{Month}`, `{Season}`, `{Day}`, `{Year}`, `{MonthNumber}`, and
`{DayOfYear}`.

Use the native tab's top-right **Reset to Defaults** button to restore calendar
settings. The old duplicate reset row is intentionally not shown.

When the optional MCM settings page successfully loads, the native **Calendar**
tab remains visible but is disabled so there is one active settings screen. If
MCM is not installed or its optional adapter cannot initialize, the native tab
remains fully functional.

Standalone settings are saved to:

```text
Documents\Mount and Blade II Bannerlord\Configs\RealisticCalendarTweaks\settings.xml
```

The file is created automatically on first launch. Every calendar setting is
kept there, including the twelve month names, month lengths, and annual-balance
toggles. For example:

```xml
<RealisticCalendarTweaks UseLeapYears="true" ShowDayLabel="false" ShowYearLabel="false"
  UseOrdinalDaySuffixes="true"
  AutoCampaignTimeScale="true" CampaignTimeScale="0.23"
  FastForwardSpeedMultiplier="4"
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
MCM when MCM is installed.
The date-format selector uses **Month-Day-Year** as the default and also provides
**Day-Month-Year** and **Year-Month-Day**. The season name is always displayed
to the right of the map clock, regardless of the selected date order. Changes
apply immediately to display settings; campaign-time pacing changes affect
subsequent campaign-time advancement.

Positive renown rewards are multiplied by `RenownGainMultiplier`, which defaults
to `0.5` and can be changed from the Calendar settings tab, MCM, or XML.

The five annual-balance toggles and Lord Death Rate Multiplier are available in
XML, optional MCM, and the native Calendar Options tab. They are campaign-start
settings: changing one after a campaign session has started is ignored and
logged, so the module does not attempt unsupported hot-swapping or rewrite
existing quest deadlines. Fast-Forward Speed Multiplier is the exception: it
uses Bannerlord's built-in speed property and may be changed live from 1x to
128x; normal map pace remains fixed.

For the 365-day calendar, the balance layer scales identified native daily
economy, settlement, progression, and probability systems by the native-84-day
to 365-day ratio so their approximate annual behavior remains comparable.
Diagnostics record applied patches and compatibility fallbacks. XML, MCM, and
the native Calendar Options tab all use the same persisted settings state.
