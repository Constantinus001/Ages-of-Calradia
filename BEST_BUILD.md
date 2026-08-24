# Current best build

Accepted by the user on 2026-08-23 and published as v1.5.11.

## Approved archives

- Release: `artifacts/AgesOfCalradia-v1.5.11.zip`
  - SHA-256: `4293BF62693DFA5DED0CFFFA53A3246C7EE42E649782A34D42F042115B61C608`
- Test: `artifacts/AgesOfCalradia-v1.5.11-Test.zip`
  - SHA-256: `8FB203D63B9FF9BF3E9905A72E3B5FF537F127CA914AC35C6ABFE19098398622`

## Immutable visual baseline

`bin/Win64_Shipping_Client/AgesOfCalradia.dll`

SHA-256: `560F1B5181F8CC2EFE51564D8675FD3089E722606FA55B0B166D36ECD9868D8E`

This exact main DLL contains the user-approved political fill. Do not rebuild
or replace it when restoring or extending v1.5.11.

Calendar and UI corrections are isolated in sidecars:

- `AgesOfCalradia.Approved560CalendarFixes.dll`
  - SHA-256: `10DCD1C896272B2EAD045C7C575293ADD66742C81F7CFD43199558311AB3EF37`
- `AgesOfCalradia.CampaignLabelVisibility.dll`
  - SHA-256: `59F9773D7F0B224FCA0109D0BAC8C9FECCD5ECC3663A0D683D0B6E97D06FDCD5`

The release package excludes diagnostics and symbols. The test package adds
World Events alignment diagnostics plus matching PDBs for the two rebuilt
sidecars; it deliberately excludes the mismatched current-source main PDB.
