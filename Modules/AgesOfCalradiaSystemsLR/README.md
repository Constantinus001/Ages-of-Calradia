# AOC SYSTEMS L & R

This runtime module combines Logistics with Refuges and portable camps. It
loads after `AOC CORE` and retains the existing
`AgesOfCalradiaLogistics.dll` and `AgesOfCalradiaRefuges.dll` assembly,
namespace, and save-key identities.

Do not enable the legacy standalone Logistics or Refuges modules alongside
this package. Existing campaigns can show Bannerlord's module-mismatch warning
because the two standalone module IDs are replaced by `AgesOfCalradiaSystemsLR`.
Back up and test an existing save before overwriting its last pre-merge slot.
