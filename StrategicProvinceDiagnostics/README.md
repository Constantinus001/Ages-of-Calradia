# Strategic Province Diagnostics

This is an independent Bannerlord module/build for investigating the 133-region
strategic map. It does not compile, load, patch, or depend on the refuge-system
code in `RealisticCalendarTweaks`.

Build it from this directory:

```powershell
msbuild StrategicProvinceDiagnostics.csproj /p:Configuration=Release
```

Copy the resulting module directory, including `SubModule.xml` and
`bin\Win64_Shipping_Client\StrategicProvinceDiagnostics.dll`, into the game's
`Modules` directory. Enable **Strategic Province Diagnostics** alongside the
normal calendar/refuge build.

Logs are written beside the diagnostics module in `Logs` when possible, with a
Documents fallback:

- `StrategicProvinceDiagnostics.log` — session summaries, every province at
  session start, state changes, duplicate mappings, and capture totals.
- `StrategicProvinceDiagnostics.tsv` — all 133 province rows at session start
  and every campaign day, suitable for spreadsheet or script analysis.

`STRIPE_ELIGIBLE` matches the composed strategic texture's stripe condition:
the mapped settlement is under siege and its siege event has a non-null
besieger faction. The TSV also records the owner-faction source, siege object
chain, map coordinates, duplicate mappings, and the reason a province is
unmapped or transparent.
