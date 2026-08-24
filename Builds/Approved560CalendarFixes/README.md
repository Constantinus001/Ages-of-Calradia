# Approved 560F1B51 calendar fixes

This compatibility sidecar applies fixes 1-6 without rebuilding or replacing
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
