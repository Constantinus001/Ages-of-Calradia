# Ages of Calradia Refuges

The separately loadable refuge and player-camp system for Ages of Calradia.

Load this module after Ages of Calradia. It preserves the existing refuge
campaign save keys, so an existing refuge remains available when this module
replaces the integrated version. The base module remains fully usable without
this optional module; its Camp button is shown only while Refuges is loaded.

## Build

```powershell
dotnet msbuild .\AgesOfCalradiaRefuges.csproj /t:Rebuild /p:Configuration=Release
& .\Tests\Verify-RefugeModule.ps1
```
