# Clock and campaign-time improvement research

## Finding

The mod does not appear to have a second independent clock. Bannerlord's
native pipeline is:

```text
Campaign.TickMapTime(realDt)
    -> selects play/fast-forward amount
    -> MapTimeTracker.Tick(seconds)
    -> CampaignTime.Now
    -> map-bar VM and world visuals
```

The local Bannerlord 1.4.5 API reference shows that `TickMapTime` calculates
`0.25 * realDt`, applies `SpeedUpMultiplier` only in fast-forward modes, and
passes `4320 * num` seconds to `MapTimeTracker`. `MapTimeTracker` converts those
seconds directly into campaign ticks.

References:

- [Native Campaign.TickMapTime](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:10193>)
- [Native MapTimeTracker.Tick](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:30754>)
- [Native MapTimeControlVM.Refresh](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:251153>)

## What the mod currently does

`MapTimeTrackerPatch` multiplies the final seconds by the default `0.15`
calendar scale. `CampaignPacingPatch` changes Bannerlord's own
`SpeedUpMultiplier` only while fast-forwarding. This is not an obvious double
multiplier: the two patches affect different parts of the native pipeline.

The custom map-bar text clock is refreshed from
`CampaignTime.Now.CurrentHourInDay`, while the native VM's `Time` property is
calculated from `CampaignTime.Now.ToHours % CampaignTime.HoursInDay`. They are
mathematically equivalent for normal positive campaign time, but they are two
different sources feeding the sundial and the visible text.

## Improvements worth making

### 1. Use one canonical hour calculation

Create a calendar helper that follows the native map-bar expression exactly:

```csharp
float hour = (float)(CampaignTime.Now.ToHours % CampaignTime.HoursInDay);
```

Use it for the custom text clock and the weather/night-time patch. This removes
the possibility of the text and native sundial disagreeing because of different
rounding or future engine changes. It also makes the source easy to log and
test. This is a consistency fix, not a change to the campaign-time scale.

### 2. Add a clock diagnostic mode before changing pacing

When the map clock is reported as wrong, log one sample every real second with:

- `Campaign.Current.TimeControlMode`;
- `Campaign.Current.SpeedUpMultiplier`;
- `CampaignTime.Now.ToDays`, `ToHours`, and `CurrentHourInDay`;
- `CampaignTime.DeltaTime.ToSeconds`;
- the displayed hour/minute string; and
- the native inherited `MapTimeControlVM.Time` value.

This separates four different bugs that look similar in-game: time stopped,
time advancing too fast, the text display lagging, and the map lighting being
out of sync with the clock.

### 3. Test expected pacing numerically

At normal play, Bannerlord's native formula produces 18 native campaign hours
per real minute before the mod scale. With the new default `0.15` scale, the
expected result is approximately **2.70 campaign hours per real minute**. A
normal campaign day should therefore take about **8.9 real minutes**.

At the default 4x fast-forward setting, the expected rate is approximately
**10.80 campaign hours per real minute**. Pause should produce zero campaign
delta. These measurements should be taken from `CampaignTime.DeltaTime`, not
from how quickly the text label repaints.

### 4. Keep the native time-control state authoritative

The existing fast-forward patch should continue setting Bannerlord's
`SpeedUpMultiplier`; it should not multiply `realDt` or patch both
`TickMapTime` and `MapTimeTracker` with the fast-forward value. The current
division of responsibility is sound:

- `MapTimeTrackerPatch`: calendar-scale conversion;
- `CampaignPacingPatch`: native fast-forward multiplier;
- `MapBarSeasonDataSourcePatch`: display only.

If the clock is too fast only during fast-forward, inspect the actual mode and
`SpeedUpMultiplier` first. If it is too fast during normal play, inspect the
tracker prefix and Harmony patch audit for duplicate owners.

### 5. Separate clock text from map lighting diagnosis

The former custom weather override has been reduced to a pass-through. Native
weather now owns the night-factor curve and consumes the same sunrise/sunset
fields as the rest of the engine, removing a second visual clock.

The native API reference reports campaign sunrise/sunset values of 2 and 22 in
the supported model. The custom patch normally reads the campaign model, but
its fallback values are 6 and 18, so initialization-time lighting should be
checked separately.

## Acceptance matrix

1. Pause for 60 real seconds: campaign delta remains zero.
2. Normal play for 60 real seconds: delta is approximately 2.70 hours.
3. Fast-forward for 60 real seconds: delta is approximately 10.80 hours.
4. Cross 23:59 to 00:00: text clock, sundial, date, and day/night state agree.
5. Save and reload at a non-integer minute: the clock does not jump backward
   or advance twice.
6. Enter and leave a wait menu: progress and campaign delta agree.
7. Compare dawn/dusk visual transitions with the numeric clock.
8. Verify the clock at 12:00, 12:59, 23:59, and both 12-hour AM/PM boundaries.

## Conclusion

The first improvement should be canonicalizing the displayed hour and adding
diagnostics, not changing the pacing value blindly. The API evidence
supports the current broad time-flow architecture; the most likely remaining
problems are display-source mismatch, fast-forward state handling, or weather
visuals being mistaken for clock errors.

## Fast-forward synchronization fix applied

The continued audit confirmed that the custom map-bar VM cannot override the
native `Tick()` method: Bannerlord declares it non-virtual. The fix therefore
hooks the native map-time tick and refreshes the replacement VM after the base
tick has run.

- During fast-forward, the custom date, season, numeric clock, and inherited
  sundial hour are refreshed from `CampaignTime.Now` whenever campaign time
  advances by at least 0.05 hours (three campaign minutes).
- `RefreshClock()` now writes the inherited `MapTimeControlVM.Time` property
  beside the custom text. The sundial and text no longer depend on separate
  stale values.
- Leaving fast-forward clears the refresh watermark, so the next fast-forward
  interval immediately resynchronizes.
- The native speed multiplier and campaign-time advancement are unchanged;
  this fixes display synchronization rather than altering pacing.

Implementation reference:
[map-bar fast-forward synchronization](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/MapBarSeasonDataSourcePatch.cs:90>)

Runtime verification still needs an in-game pass: pause, normal play,
fast-forward, crossing midnight, and save/reload should be checked against the
acceptance matrix above. The build and calendar math checks pass locally.

## Additional findings from the continued audit

### 6. Camp “rest until dawn” is not using Bannerlord’s dawn

This is a confirmed semantic mismatch in the current code. The camp behavior
defines `DawnHour = 7f` and calculates the wait target from that value:

- [CalendarCampBehavior dawn constant](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:32>)
- [CalendarCampBehavior dawn calculation](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:448>)

The supported native campaign model reports sunrise at 02:00 and sunset at
22:00, and `CampaignTime.Initialize` copies those model values into the
native static clock fields:

- [Native sunrise/sunset defaults](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:55385>)
- [Native CampaignTime initialization](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:40619>)

Improvement: calculate “until dawn” from `CampaignTime.SunRise` (or the
campaign time model during initialization) instead of a private 07:00
constant. Keep the existing one-hour minimum only if testing confirms it is
needed for the wait-menu completion event. This should be fixed before tuning
the clock because a player can reasonably interpret the extra five hours as a
clock problem.

### 7. Campaign lighting can follow the configurable visual clock

Bannerlord's native weather model uses the campaign hour, but its visual
daylight curve is anchored to native sunrise/sunset (02:00/22:00). That means
the clock can show Night while the map still looks like dawn, or show Evening
while the native atmosphere is already dark.

- [Custom weather/night patch](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/MapWeatherNightFactorPatch.cs:1>)

The native weather model uses `CampaignTime.SunRise` and `SunSet` for the
day/night calculation and has a separate two-hour exposure transition around
both boundaries:

- [Native night-factor calculation](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:62796>)
- [Native dawn/dusk exposure transition](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:62806>)

The implementation now adds an opt-in-compatible visual layer after native
weather calculation. It keeps precipitation, weather colors, and third-party
weather-model replacement intact while adjusting luminous atmosphere values
to the configured visual sunrise, sunset, and transition hours. Native
`CampaignTime.SunRise`, `SunSet`, and `IsDayTime` remain unchanged, so gameplay
mechanics do not inherit the visual profile.

Defaults are visual sunrise 05:00, visual sunset 21:00, and one-hour gradual
dawn/dusk transitions. The standalone XML keys are:

```xml
ClockSynchronizedLighting="True"
VisualSunriseHour="5"
VisualSunsetHour="21"
VisualLightingTransitionHours="1"
```

If another mod replaces Bannerlord's `MapWeatherModel`, this patch safely does
not replace that model; disable the setting if that mod owns atmosphere output.

### 8. Leap-day boundaries need a native-consumer audit

The mod’s custom `GetDayOfYear` can return day 365 during a leap year, while
the patched `CampaignTime.DaysInYear` currently returns the common-year value
365. The underlying calendar math separately knows that a leap year has 366
days:

- [Custom leap-year length](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarTimeMath.cs:46>)
- [Patched native DaysInYear property](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CampaignTimeCalendarPatches.cs:50>)

This is an inconsistency, although its in-game impact still needs runtime
verification. The native API uses `CampaignTime.DaysInYear` in birthday/death
sampling and age-related probability calculations, so changing the global
property to 366 would also change non-leap behavior for every consumer.

Improvement: add boundary tests for February 28, February 29, March 1, and
December 31 in leap and non-leap years, then audit each native consumer. Prefer
targeted fixes or a documented “365-day annual denominator” policy over making
the global static property vary implicitly with the current date.

### 9. Daily discrete rounding is not stable across reloads

`DailyRateBalance.ScaleDiscreteDailyValue` seeds fractional daily rounding with
`RuntimeHelpers.GetHashCode(scope)` and `channel.GetHashCode()`:

- [Daily-rate discrete scaling](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/DailyRateBalancePatches.cs:73>)

Object identity is process-dependent, and string hash codes are not a safe
serialized random seed. A save/load can therefore receive a different rounded
result for the same day, entity, and channel. This is not a clock-rate issue,
but it can look like inconsistent daily simulation after reloading.

Implemented: the seed now uses a stable reflected `StringId` when available,
the campaign day, and an explicit deterministic string hash for the channel.
It falls back to a stable type name rather than process object identity, so
save/reload no longer changes the random rounding source merely because the
object was recreated.

## Revised priority

1. Use native sunrise for camp “rest until dawn”.
2. Let native weather own the night-factor curve.
3. Canonicalize hour-of-day reads for text and the sundial.
4. Make discrete annual-balance rounding stable across saves.
5. Keep leap-day/native-consumer policy as a separate compatibility audit; the
   global `DaysInYear` denominator remains intentionally 365 for annual-rate
   conversion until runtime consumer tests justify changing it.

## Human-readable time-of-day periods

Bannerlord does have the five human-readable labels in its native map-bar
tooltip. The local native reference shows the exact integer-hour rule:

- [Native time-of-day tooltip](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:233123>)

| Period | Exact display-time rule | Length |
| --- | --- | ---: |
| Morning | 06:00 through 11:59 | 6 hours |
| Noon | 12:00 through 14:59 | 3 hours |
| Afternoon | 15:00 through 17:59 | 3 hours |
| Evening | 18:00 through 21:59 | 4 hours |
| Night | 22:00 through 05:59 | 8 hours |

This native classification is separate from the gameplay `IsDayTime` rule,
which uses sunrise/sunset for mechanics and lighting. The requested custom
variant is therefore a deliberate change from vanilla: Morning 05:00–12:00,
Noon 12:00–14:00, Afternoon 14:00–18:00, Evening 18:00–21:00, and Night
21:00–05:00.

The numeric clock remains authoritative; the requested period classification is
documented for a future configurable display but is not currently shown under
the numeric map-bar clock.

Implementation guidance:

- if enabled later, the period must be derived from the same canonical hour
  used by `TimeOfDay`;
- it must remain a separate display property, so it cannot replace or distort
  the numeric clock;
- the current map-bar layout has one numeric `TimeOfDay` widget beside the
  sundial, so a compact period label or tooltip is safer than expanding the
  central panel without checking controller/navigation spacing;
- do not use `CampaignTime.IsDayTime` for these labels, because that native
  gameplay rule follows the campaign model’s 02:00/22:00 sunrise and sunset;
- test all transitions at 04:59→05:00, 11:59→12:00, 13:59→14:00,
  17:59→18:00, 20:59→21:00, and 23:59→00:00.

The existing numeric widget is visible here: [MapBar TimeOfDay widget](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/GUI/Prefabs/Map/MapBar.xml:136>).
