# Ages of Calradia Logistics

A standalone add-on module for Ages of Calradia. It is deliberately separate
while the logistics systems are developed and tested; it can be merged into the
main module later without changing its existing source tree.

## Current milestone

- Adds the merchantable Supply inventory item.
- Reuses Bannerlord's built-in crate_a mesh (the same crate visual used by
  vanilla Stolen Goods) while keeping the vanilla item unchanged.
- Persists a 0-100 reserve for each eligible lord, caravan, and player party.
  A Supply crate contributes 20 reserve when loaded through the forthcoming
  logistics interface; the battle system will spend this finite reserve.
- Town markets replenish Supply crates gradually, up to 3 per normal town and
  5 in prosperous towns. At a town, use **Load supplies into baggage train**
  to consume carried crates and refill the player party reserve.
- In player field battles, every non-bandit side with an eligible lord,
  caravan, or player party spawns a physical baggage train using the native
  cart prefabs and scattered-goods props. Twelve forced wagons and eight
  ground supply piles form an irregular, rotated baggage area 20m behind the
  side's deployment line; the central wagon retains a 6m resupply radius.
- After battle deployment, up to eight existing troops are detached into an AI
  guard formation and ordered to hold at their side's central wagon.
- Wagon visuals rotate through every native intact cart, cargo-heap, hay-cart,
  and olive-cart variant. Broken carts are reserved for future train-damage
  states.
- Every 3 seconds, agents within their own green ring can receive up to three
  rounds for one reserve point. This includes the player; it stops immediately
  when the side's finite reserve is empty.

## Planned implementation order

1. Persist each eligible party's reserve and consume/refill it from Supply.
2. Spawn one baggage train per eligible battle side.
3. Add a green ground-range indicator and finite, proximity-based resupply.
4. Assign train guards, allow enemy raids/capture, and apply morale effects.
5. Add winter logistics effects and the Refuge Quartermaster reward loop.

## Load order

Load after Native, SandBoxCore, Sandbox, and AgesOfCalradia.

## Build and verify

Run:

    dotnet msbuild .\AgesOfCalradiaLogistics.csproj /t:Rebuild /p:Configuration=Release
    .\Tests\Verify-SupplyItem.ps1

The compiled DLL is written to bin\Win64_Shipping_Client. For a manual game
test, copy this module folder to Bannerlord's Modules directory, excluding
intermediate build folders.

## Diagnostics

The module writes bounded diagnostics to:

    %LOCALAPPDATA%\Mount and Blade II Bannerlord\Logs\AgesOfCalradiaLogistics.log

When it exceeds 2 MB, the previous log is retained as
AgesOfCalradiaLogistics.log.previous. It records reserve loading, market
restocks, train spawn decisions, range-marker creation, and 30-second battle
resupply summaries.

## Battle test

Start a new player-involved **field battle** after loading the module. Town and
siege missions intentionally do not create baggage trains.

If a battle crashes, retain the newest folder under
%PROGRAMDATA%\Mount and Blade II Bannerlord\crashes and the logistics log.
The train spawn diagnostics include deployment frame and native-prefab
checkpoints for diagnosis.
