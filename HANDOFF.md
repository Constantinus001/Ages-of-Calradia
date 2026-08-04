# Realistic Calendar Tweaks - Handoff

## Current release

- Repository: `https://github.com/Constantinus001/Realistic-Calendar-Tweaks`
- Current public release: `v1.4.6`
- Release page: `https://github.com/Constantinus001/Realistic-Calendar-Tweaks/releases/tag/v1.4.6`
- Release commit: `18c2bb2f569fd27c2983042b2418e413af60d272`
- Bannerlord target: Native `v1.4.7`
- Release ZIP SHA-256:
  `FE47E34FA78E03C7D515284E7CE366F85BEFD36D00FEF0047BEF9EECA0C69275`

## Live installation

The current live module is installed at:

```text
C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\RealisticCalendarTweaks
```

It contains one module only:

```text
Id: RealisticCalendarTweaks
Name: Realistic Calendar Tweaks
Version: v1.4.6
```

`_TwelveMonthCalendar` is intentionally absent from the v1.4.6 live layout and
release archive. Prior live layouts were moved recoverably to:

```text
C:\Program Files\Steam\steamapps\common\Mount & Blade II Bannerlord\ModBackups
```

## Compatibility

New saves use a primitive soft campaign profile; the calendar no longer writes
its previous hard module-lock marker.

v1.4.6 intentionally does not ship a legacy `_TwelveMonthCalendar` bridge. If
an older calendar save needs its one-time migration path, retain/download v1.4.5,
load and re-save with its bridge, then move to v1.4.6. Do not add the old module
folder to the v1.4.6 release ZIP.

## Main gameplay/settings behavior

- Calendar: fixed Gregorian 365-day year, configurable month/season names and
  month lengths totaling 365, optional Gregorian leap years.
- Normal map pace: fixed at the Gregorian base cadence.
- Fast forward: one live-safe `FastForwardSpeedMultiplier` setting, 1x-128x,
  default 4x. It updates Bannerlord's own `Campaign.SpeedUpMultiplier`; do not
  add a second `TickMapTime` multiplier.
- Lord mortality: `LordDeathRateMultiplier` defaults to `0.20`. It affects only
  eligible AI noble lords' ordinary old-age and battle mortality. Executions,
  scripted deaths, and the player are unchanged.
- MCM is optional. When MCM registers successfully, the native Calendar tab is
  intentionally disabled to avoid duplicate settings surfaces. Without MCM,
  the native Calendar tab remains active.
- Standalone XML settings live at:

```text
Documents\Mount and Blade II Bannerlord\Configs\RealisticCalendarTweaks\settings.xml
```

Campaign-start simulation settings are locked after a campaign session starts;
display settings and fast-forward speed remain live-safe.

## Diagnostics

Primary log:

```text
<Bannerlord>\Modules\RealisticCalendarTweaks\Logs\RealisticCalendarTweaks.log
```

If the game cannot write in its install directory, diagnostics fall back to:

```text
Documents\Mount and Blade II Bannerlord\Configs\ModLogs
```

Crash snapshots are retained under the log directory's `CrashReports` folder.

## Build and release workflow

From this repository root:

```powershell
dotnet msbuild TwelveMonthCalendar.csproj /t:Rebuild /p:Configuration=Release
dotnet msbuild TwelveMonthCalendar.MCM.csproj /t:Rebuild /p:Configuration=Release
.\Tests\Verify-CalendarMath.ps1
```

Before publishing, commit the exact source and run the release gate from a
clean worktree:

```powershell
.\Tests\Verify-Release.ps1
```

The release gate builds both DLLs, checks calendar/profile math, verifies the
exact single-module ZIP file list, rejects Better Time and legacy bridge files,
and scans the final ZIP with Microsoft Defender. Do not upload a release unless
the script reports `PASS`. For a quick manual check, use
`-CloudVerdictHoldMinutes 1`; use the default 10-minute hold for a normal public
release.

Release artifacts are written beneath `artifacts`. Upload the exact scanned ZIP
and include its SHA-256 in GitHub release notes.

## Important source-layout note

The project file remains named `TwelveMonthCalendar.csproj` for development
continuity, but the compiled runtime assemblies are:

```text
RealisticCalendarTweaks.dll
RealisticCalendarTweaks.MCM.dll
```

The public module ID and display name must remain `RealisticCalendarTweaks` and
`Realistic Calendar Tweaks` unless a deliberate save-compatibility plan is made.
