# Agent instructions

Before changing code, read [`docs/CODE_QUALITY.md`](docs/CODE_QUALITY.md).

## Required workflow for every code change

1. Inspect the relevant code, tests, assets, and documentation before editing.
2. State the affected architectural boundary, compatibility risk, and intended
   verification when the change is more than a trivial edit.
3. Make the smallest focused change that satisfies the task.
4. Update the relevant test, contract check, or documentation when behavior or
   an architectural decision changes.
5. Run the Release build and the narrowest relevant verification checks.
6. Report what changed, what was verified, and any remaining failure or blocker.

## Required rules

- Preserve the boundaries between calendar/core logic, game integration, UI,
  persistence, diagnostics, and feature systems.
- New Harmony patches must document their native target, purpose, compatibility
  risk, failure behavior, and verification coverage.
- Do not add broad `catch (Exception)` handling unless crossing a Bannerlord,
  mod, file, or reflection boundary. Log the exception and fail safely.
- Keep new classes focused. Split a class when it approaches 800 lines or has
  more than one substantial responsibility.
- Keep settings and save/profile changes backward compatible unless the task
  explicitly changes the compatibility contract.
- Add or update a targeted verification check for behavior changes.
- Do not leave compiler warnings, dead code, temporary fallbacks, or debug
  output in a release path.
- Run the Release build and the relevant verification scripts before declaring
  a change complete. Record failures and environmental blockers clearly.
- Avoid unrelated refactors while implementing a feature. Refactor large
  existing classes in separately reviewable steps.

## Project-specific cautions

- Bannerlord APIs are version-sensitive; verify Harmony targets and optional
  integrations before relying on them.
- Native game calls, reflection, texture creation, mission setup, and save
  compatibility code must have a safe failure path and diagnostics.
- Production and diagnostics/test builds must remain intentionally separated.
- Treat `Tests\Verify-Release.ps1` as the release gate, not merely a packaging
  script.
