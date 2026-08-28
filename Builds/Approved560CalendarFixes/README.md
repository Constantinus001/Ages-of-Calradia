# Approved 560F1B51 combined fixes for v1.5.14

This compatibility sidecar applies the combined calendar and economy fixes without rebuilding or replacing
the user-approved `AgesOfCalradia.dll` whose SHA-256 begins `560F1B51`.

It must load after the three main-DLL submodules. It does not patch political
map rendering, territory fill, island masks, lake classification, materials,
meshes, or map textures.

The sidecar also keeps town/city labels enabled at every zoom level of the
World Events UI strategic map. Castles remain icon-only, and the UI prefab
provides the larger readable label style.

At startup it validates the approved DLL's internal calendar bridge and every
required Bannerlord target before patching. Failure disables the sidecar and
leaves the approved main DLL unchanged.

The coordinated food correction runs village food production, food-category
market demand, food budgets, food price smoothing, and food workshops at
Bannerlord's native daily cadence. It then applies the Gregorian factor once
to the completed town food surplus or deficit. Prosperity growth and all other
annual-balance systems remain annualized. Food-market turnover therefore stays
internally consistent across the 365-day calendar without scaling supply or
demand twice.

Existing tournament, workshop, wage, finance, war-cadence, scene-date,
campaign-time, and strategic-label corrections remain active. The approved
main DLL retains its save-age compatibility behavior.
