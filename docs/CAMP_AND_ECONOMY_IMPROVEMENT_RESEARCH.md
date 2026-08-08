# Camp and town-economy patch notes

## Towns with zero gold

Bannerlord updates town gold in the daily settlement-economy tick. The native
flow is:

```text
DailyTickTown
    -> GetTownGoldChange(town)
    -> town.ChangeGold(change)
    -> gold is clamped at zero
```

The local native reference shows the default correction formula:

```text
round(0.25 * (10000 + prosperity * 12 - town.Gold))
```

References:

- [Native GetTownGoldChange formula](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:67864>)
- [Native daily town update](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:195344>)
- [Native zero-gold clamp](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/bannerlord modding documentation/api_v1.4.5.txt:162707>)

The calendar patch previously scaled every town-gold correction by the
0.23 annual-rate factor. That is reasonable for ordinary daily income, but
town gold is also a market-liquidity buffer. A town already at zero could
therefore recover too slowly after a large market outflow.

The local patch now preserves the native positive recovery for a town whose
gold is zero, while normal towns retain the Gregorian annualized correction:

- [Town-gold safeguard](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/DailyRateBalancePatches.cs:511>)

This does not inject money or set a fixed balance. It only prevents the
annual-rate conversion from weakening the native emergency recovery. Runtime
validation should record, for affected towns, gold before the daily tick,
native change, scaled change, and gold after the tick. If zeros persist, the
next suspect is another module or transaction path draining gold after the
economy tick.

## Camp wait menu and temporary siege tent

The requested camp behavior is already represented in the local patch:

- the camp menu registers “Wait here for some time” and “Rest until dawn”;
- both enter a native wait menu that advances from campaign-time `dt`;
- the wait menu ends by returning to the camp menu;
- a temporary native `map_icon_siege_camp_tent` is instantiated at the party;
- the tent is removed when resting stops, finishes, or camp is left.

References:

- [Camp wait-menu registration](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:97>)
- [Wait-menu time progression](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:500>)
- [Temporary siege-tent creation](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:559>)
- [Temporary siege-tent cleanup](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarCampBehavior.cs:608>)

The sunrise target was also changed from a private 07:00 constant to
Bannerlord's native campaign sunrise, keeping the camp action consistent with
the rest of the game.

## Acceptance checks

1. Find a town at zero gold and advance one daily tick; verify it receives a
   positive recovery instead of remaining empty.
2. Verify towns above zero still use the annualized correction.
3. Enter camp: one siege tent appears at the party position.
4. Wait for a short duration: the progress bar and campaign time advance
   together.
5. Stop waiting, finish waiting, and leave camp; verify no duplicate or
   orphaned tent remains.
6. Enter an encounter or settlement visit; verify camp and tent creation are
   disabled.
