# Current best build

The v1.5.12 patch preserves the user-accepted v1.5.11 visual baseline and adds
only the guarded combined-fix sidecar update.

## Approved archives

- Release: `artifacts/AgesOfCalradia-v1.5.12.zip`
  - Size: 38,187,151 bytes (36.42 MiB)
  - SHA-256: `333737B6B8053FE0915CB14DD35159CC7BCD9BBE5908581E11B73480D4BEFDEC`
- Test: `artifacts/AgesOfCalradia-v1.5.11-Test.zip`
  - SHA-256: `8FB203D63B9FF9BF3E9905A72E3B5FF537F127CA914AC35C6ABFE19098398622`

## Immutable visual baseline

`bin/Win64_Shipping_Client/AgesOfCalradia.dll`

SHA-256: `560F1B5181F8CC2EFE51564D8675FD3089E722606FA55B0B166D36ECD9868D8E`

This exact main DLL contains the user-approved political fill. Do not rebuild
or replace it when restoring or extending v1.5.12.

Calendar and UI corrections are isolated in sidecars:

- `AgesOfCalradia.Approved560CalendarFixes.dll`
  - Version: `1.5.12.0`
  - SHA-256: `5187E07E2D323CB801CFF030D2D03F0EA9FBC335CDC03E97ECA695E94F24F2A7`
- `AgesOfCalradia.CampaignLabelVisibility.dll`
  - SHA-256: `59F9773D7F0B224FCA0109D0BAC8C9FECCD5ECC3663A0D683D0B6E97D06FDCD5`

The v1.5.12 release package excludes diagnostics, logs, and symbols. The older
v1.5.11 test package remains available separately and is not part of v1.5.12.
