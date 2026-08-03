# Building Twelve Month Calendar

Build against Bannerlord v1.4.7 with either MSBuild properties or environment variables:

```powershell
dotnet msbuild TwelveMonthCalendar.csproj /t:Rebuild /p:Configuration=Release /p:BannerlordDir='C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord'
```

`BANNERLORD_DIR` can supply the same location when `/p:BannerlordDir` is omitted. `NETSTANDARD_PATH` can override the legacy .NET Standard facade path when required by a different development machine.

Run `Tests\Verify-Release.ps1` only from a clean committed source tree before publishing. It builds both optional configurations, runs calendar checks, validates the exact package contents, and scans the final archive with Microsoft Defender.
