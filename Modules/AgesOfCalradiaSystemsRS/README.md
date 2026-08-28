# AOC SYSTEMS R & S

This runtime module combines Religions/population with hereditary Succession.
It loads after `AOC CORE` and deliberately loads Religions before Succession,
because Succession consumes the Religion assembly's public service.

The existing `AgesOfCalradiaReligions.dll` and
`AgesOfCalradiaSuccession.dll` assembly, namespace, Harmony, and save-key
identities are preserved. Do not enable either legacy standalone module with
this package. Back up and test existing campaigns because Bannerlord can show
a module-mismatch warning when their recorded standalone IDs are replaced by
`AgesOfCalradiaSystemsRS`.
