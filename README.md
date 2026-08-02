# Twelve Month Calendar

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
- party map speed left at native values; the previous speed compensation was
  removed because it made parties move approximately 4.35x too fast
- pregnancy due dates set to nine configured calendar months after conception
- positive renown rewards reduced to 50% by default to slow clan-tier progression

The date is changed by patching `CampaignTime.ToString()`, which is also what
the campaign map time-control UI uses for its visible date. This means dates in
other native messages that rely on `CampaignTime.ToString()` use the same format.

## Build

Open `TwelveMonthCalendar.csproj` in Visual Studio, or run:

```powershell
msbuild TwelveMonthCalendar.csproj /p:Configuration=Release
```

The build output is written to `bin\Win64_Shipping_Client`. Copy the module
folder into the game's `Modules` directory, then enable **Twelve Month Calendar** in the
Bannerlord launcher.

This changes the underlying campaign-time interpretation so hero aging, native
four-season calculations, and campaign events created with
`CampaignTime.Years()` follow the 365-day year. Day-based timers remain
day-based. Start a new campaign with the module enabled. Existing saves use
Bannerlord's original 84-day year and are not compatible with this calendar
without a save migration layer.

Leap years use the Gregorian rule: divisible by 4, except century years unless
they are also divisible by 400. The campaign's starting year, 1084, is treated
as a leap year.

The time multiplier slows the campaign clock, not the number of days in the
calendar. Daily settlement, progression, and finance rates are reduced per
campaign day by the native-84-day to 365-day ratio, preserving their
approximate annual rates. Party map speed remains at native values, and
pregnancy defaults to nine calendar months from the moment conception starts.

## Diagnostics

The module writes diagnostics to:

```text
Documents\Mount and Blade II Bannerlord\Configs\ModLogs\TwelveMonthCalendar.log
```

The log records module loading, Harmony patch registration, campaign startup,
the current date/season conversion, and a main-hero age check. It does not log
every campaign tick. It also records the active campaign-time multiplier.

## Optional MCM settings

MCM is optional. If MCM is installed, the calendar exposes an in-game settings
page with calendar system, leap-year, display-label, campaign-time, and date
format settings. The date format supports `{Month}`, `{Season}`, `{Day}`,
`{Year}`, `{MonthNumber}`, and `{DayOfYear}`. Without MCM, the native
**Calendar** Options tab provides the same core controls. The season is always
shown first and dates use spaces rather than hyphens (for example
`Spring April 3rd 1084`). The preset default is **Month-Day-Year**.

## Standalone in-game settings

MCM is not required. The module adds calendar-system, leap-year, label, pacing,
and preset-format controls to a separate **Calendar** tab in Bannerlord's native
**Options** screen. Open the game's Options screen and select the Calendar tab.
MCM and the XML settings file also support custom format
tokens: `{Month}`, `{Season}`, `{Day}`, `{Year}`, `{MonthNumber}`, and
`{DayOfYear}`.

Standalone settings are saved to:

```text
Documents\Mount and Blade II Bannerlord\Configs\TwelveMonthCalendar\settings.xml
```

The file is created automatically on first launch. Every calendar setting is
kept there, including the twelve month names and month lengths. For example:

```xml
<TwelveMonthCalendar CalendarSystem="Gregorian12Month"
  UseLeapYears="true" ShowDayLabel="false" ShowYearLabel="false"
  UseOrdinalDaySuffixes="true"
  AutoCampaignTimeScale="true" CampaignTimeScale="0.2299842"
  DateFormat="{Month} {Day} {Year}"
  NativeDaysInYear="84" UseCalendarMonthPregnancy="true"
  PregnancyDurationMonths="9" PregnancyDurationDays="273.75" RenownGainMultiplier="0.5"
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
changes to take effect. Month names are limited to 24 characters and month lengths must be between 1 and 1000; leap years
add one day to the configured second month. Changing month lengths changes the
calendar year length and therefore the pacing/balance factor. Set
`UseCalendarMonthPregnancy="false"` to use `PregnancyDurationDays` instead.

The settings are not added to campaign, town, castle, or village menus. They are
available on the separate native **Calendar** tab in the Options screen, and in
MCM when MCM is installed.
The date-format selector uses **Month-Day-Year** as the default and also provides
**Day-Month-Year** and **Year-Month-Day**. The season name is always displayed
before the date, regardless of the selected date order. Changes apply immediately
to display settings; campaign-time pacing changes affect subsequent campaign-time
advancement.

Positive renown rewards are multiplied by `RenownGainMultiplier`, which defaults
to `0.5` and can be changed from the Calendar settings tab, MCM, or XML.

For the 365-day calendar, the balance layer scales identified native daily
economy, settlement, progression, and probability systems by the native-84-day
to 365-day ratio so their approximate annual behavior remains comparable.
Diagnostics record applied patches and compatibility fallbacks. XML, MCM, and
the native Calendar Options tab all use the same persisted settings state.
