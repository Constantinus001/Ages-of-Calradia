# Building Ages of Calradia

Build against Bannerlord v1.4.7 with either MSBuild properties or environment variables:

```powershell
dotnet msbuild TwelveMonthCalendar.csproj /t:Rebuild /p:Configuration=Release /p:BannerlordDir='C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord'
dotnet msbuild TwelveMonthCalendar.MCM.csproj /t:Rebuild /p:Configuration=Release
```

Build the main project first because the MCM adapter references its output. The
MCM build copies `MCMv5.dll` beside `AgesOfCalradia.MCM.dll`; both are
declared in `SubModule.xml` and belong in the runtime package. The standalone
MCM UI module remains optional.

`BANNERLORD_DIR` can supply the same location when `/p:BannerlordDir` is omitted.
`NETSTANDARD_PATH` can override the legacy .NET Standard facade path when
required by a different development machine.

Run `Tests\Verify-Release.ps1` only from a clean committed source tree before publishing. It builds the main and optional MCM configuration, runs calendar and strategic-map checks, validates the complete single-module archive (including runtime ModuleData and complete refuge scenes), and scans the final archive with Microsoft Defender.
