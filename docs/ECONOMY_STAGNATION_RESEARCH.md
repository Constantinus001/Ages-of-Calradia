# Economy stagnation research and original improvement plan

This note compares the public descriptions and changelogs of the linked economy
mods with the current Twelve Month Calendar code. It is a design study only:
their code, assets, names, and implementation details should not be copied.
The goal is to reproduce useful economic feedback with our own smaller,
save-safe systems.

## Sources reviewed

- [Bannerlord - Realistic Economy and Market Prices](https://www.nexusmods.com/mountandblade2bannerlord/mods/11481)
- [Bannerlord Living Economy](https://www.nexusmods.com/mountandblade2bannerlord/mods/10796)
- The first supplied link, mod `12521`, did not return a readable Nexus page
  through the available research tools, so no claims about that mod are made
  here. Its title or description can be added later if supplied.

The public description of Realistic Economy and Market Prices emphasizes
optional inflation, lord-money pressure on prices, a high-price cutoff and
divider, reduced sale proceeds, and a town-gold test intended to prevent gold
from appearing without an economic source. Its changelog also mentions a
low-price crash fix and optional settings.

Living Economy describes a much broader simulation: village-to-town supply,
regional demand, seasons, route danger, caravan logistics, local treasuries,
tax policies, corruption, infrastructure, investments, economic events,
specialization, wartime procurement, and lord-wealth control. Its changelog
also stresses caps, gradual recovery, optional systems, save-safe state, and
compatibility modes.

## What our mod currently does

The current economy layer is primarily a calendar-rate conversion layer:

- `SettlementBalanceMath.DailyRateFactor` converts Bannerlord's native 84-day
  annual cadence to the extended calendar.
- Daily village production, workshop production, town prosperity, food demand,
  market budget, and supply/demand smoothing are adjusted for that cadence.
- Town gold normally receives the annualized correction. A zero-gold town keeps
  the native positive recovery for that tick, while a healthy low-gold town
  receives a bounded blend toward native recovery so its market can become
  liquid again without a fixed cash injection.
- Clan finance, caravan income, workshop income, taxes, and selected daily
  settlement values are routed through the same rate-balance logic.
- Monthly telemetry records town food, prosperity, loyalty, security, militia,
  town gold, clan gold, caravans, patrols, and bandits.

Useful local references:

- [Annual-rate balance math](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/SettlementBalancePatches.cs:10>)
- [Demand, market budget, smoothing, and town-gold patches](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/DailyRateBalancePatches.cs:440>)
- [Town-gold zero-recovery safeguard](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/DailyRateBalancePatches.cs:511>)
- [Safe scaled clan finance wrapper](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarClanFinanceModel.cs:39>)
- [Monthly balance telemetry](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CalendarBalanceTelemetry.cs:18>)

This fixes time-scale distortion, but it does not create a reason for money,
goods, caravans, or settlements to react to one another. That is why the
economy can still feel stagnant even when its daily values are correctly
annualized.

## First original implementation trial

The first trial is deliberately narrow and does not copy code or assets from
the researched mods:

- [Town liquidity recovery](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/DailyRateBalancePatches.cs:511>) keeps the native gold change as the ceiling, blends up to 75% toward that native recovery when gold is below 2,500, and refuses the recovery blend during siege, starvation, or food stocks below 25.
- [Caravan shortage priority](<D:/AI-Related Apllications & Modding/Modding/Bannerlord Modding Stuff/_TwelveMonthCalendar/CaravanTradePriorityPatch.cs:1>) adjusts Bannerlord's existing private town trade score by at most 35%. It uses low food and low gold as pressure signals; native distance, cargo, ownership, navigation, and siege checks still choose the legal route.
- The caravan hook is score-based rather than destination-forcing. This avoids teleporting goods, creating money, or overriding the engine's route validation.

This is a testable feedback loop, not a complete economy overhaul. It should be
judged by whether low-liquidity towns recover and whether caravan arrivals
increase where shortages are real, while total town gold and prices remain
bounded.

## Main diagnosis

The missing feature is a bounded feedback loop:

```text
local health and war
        -> goods demand and supply deficits
        -> prices, caravan priorities, and town liquidity
        -> taxes and market activity
        -> maintenance and reinvestment
        -> recovery or continued decline
```

Vanilla has pieces of this loop, but most of the state is implicit and there is
no persistent local treasury or transparent reinvestment controller in this
mod. A safe improvement should make the loop visible and measurable before it
adds more mechanics.

## Recommended original design

### Phase 1: liquidity and diagnostics

This is the safest first patch and directly targets towns with no gold.

1. Add a `TownEconomySnapshot` keyed by settlement ID. Store only compact,
   save-safe values: last gold, food, prosperity, security, loyalty, market
   deficit score, last caravan delivery day, and a short reason code.
2. Keep native town-gold recovery as the base. Apply a bounded health modifier
   to the recovery speed rather than assigning a new balance. Healthy,
   connected towns recover toward a target more quickly; starving, blockaded,
   or recently raided towns recover slowly but never become permanently stuck.
3. Distinguish three flows in telemetry: native town-gold change, player/party
   market withdrawals, and settlement income. This will show whether a town is
   actually failing to generate money or is being drained after the economy
   tick.
4. Add a configurable emergency floor for market liquidity only when the town
   has a genuine positive native recovery. Do not create gold on a negative
   native result, and do not use a permanent fixed town balance.

Acceptance targets:

- A zero-gold town recovers after a few daily ticks unless it is actively
  starving, besieged, or being drained by another transaction.
- The log explains why a town remains low instead of silently reporting zero.
- Total town gold does not grow faster merely because the diagnostic layer is
  enabled.

### Phase 2: bounded supply and reinvestment

This is the most valuable anti-stagnation layer after diagnostics.

- Calculate a town deficit score from food shortage, missing workshop inputs,
  low prosperity, route danger, and recent raid/siege pressure.
- Give caravans a small priority bonus for a high-deficit destination, with a
  cap and a cooldown so every caravan does not converge on one town.
- Reserve a configurable share of real town tax income as a local maintenance
  budget. The reserve can pay for roads, patrol posts, granaries, caravanserai,
  workshop support, or militia recovery over time.
- Let the owner choose a small policy set: balanced, food relief, artisan
  support, market patrols, or wartime levy. Each policy should trade one output
  against another instead of granting a universal bonus.
- Use existing gold flows for contributions and maintenance. Never duplicate
  tax income by both paying the clan and creating an equal treasury deposit.

This gives towns a reason to spend money locally. It also creates a natural
gold sink for wealthy clans without relying on arbitrary destruction of gold.

### Phase 3: regional pressure and recovery

Add these only after Phase 1 and Phase 2 are measurable:

- seasonal demand modifiers with narrow bounds;
- temporary events such as harvest failure, trade fair, drought, or mining
  boom;
- route danger that lowers delivery success and raises recovery time;
- village diversion toward nearby safe markets when the bound town is
  inaccessible or excessively hostile;
- soft lord-wealth pressure that funds holdings, escorts, or projects rather
  than deleting large amounts of gold.

The event system should use deterministic settlement/day seeds, cooldowns,
duration limits, and a world event cap. This prevents save reloads or long
campaigns from multiplying the same effect.

## What not to do

- Do not multiply every price or gold result globally. That can hide the source
  of stagnation and makes economy mods conflict with one another.
- Do not give every empty town a fixed gold injection. It cures the display but
  not the market loop and can create unlimited money.
- Do not make prosperity the only economic input. Food, security, route safety,
  war, demand, and ownership must matter independently.
- Do not add persistent state without a save-key migration/default path.
- Do not patch the same native model twice when the annual-rate layer already
  owns that conversion.

## Compatibility plan

All new systems should be independently toggleable. If another economy
overhaul is detected, the mod should either disable overlapping price,
prosperity, tax, and workshop patches or operate in a read-only diagnostics
mode. Additive features such as the strategy-map economy report can remain
active when ownership and economy models are delegated to another mod.

## Verification plan

Run controlled campaigns with the same seed and compare weekly snapshots:

1. Count towns at zero gold, low food, and negative food change.
2. Record median and 10th-percentile town gold, not only the total.
3. Track average price spread by category and the number of empty markets.
4. Count caravan arrivals, successful deliveries, raids, route disruptions,
   and towns with an active deficit.
5. Track total clan gold, total town gold, and new treasury balances together
   so money creation and money sinks are visible.
6. Compare recovery time after a raid, siege, starvation episode, and market
   drain.

The existing monthly telemetry is a good base, but it should add per-town
reason codes and a bounded sample of the lowest-liquidity towns before any
large simulation is introduced.

## Current implementation order

1. Expand telemetry and add zero-gold cause classification.
2. Test the bounded liquidity recovery already added.
3. Test the shortage-aware caravan score already added.

Local treasuries, policies, maintenance projects, and seasonal/event pressure
are deferred. They are not required for this first anti-stagnation trial and
should not be added until the telemetry shows that the smaller feedback loop
is stable.

This sequence takes the strongest idea from the linked mods—the economy must
react to local conditions—while keeping the implementation original, bounded,
and compatible with the calendar's annual-rate conversion.
