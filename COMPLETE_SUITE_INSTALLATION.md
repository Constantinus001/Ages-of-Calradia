# Ages of Calradia Complete Suite

This archive contains the complete player runtime for Bannerlord v1.4.8:

- `Ages Of Calradia` — AOC Core, calendar, World Events UI, political and
  strategic maps, compatibility fixes, and optional MCM integration.
- `AgesOfCalradiaSystemsLR` — Logistics and Refuges.
- `AgesOfCalradiaSystemsRS` — Religions, population, census/map modes, and
  hereditary succession.

## Installation

1. Close Bannerlord and its launcher.
2. Extract the archive into the Bannerlord installation directory so these
   three folders land under `Modules`.
3. In the launcher, enable **AOC CORE**, **AOC SYSTEMS L & R**, and
   **AOC SYSTEMS R & S** in that order.
4. Do not also enable the retired standalone Ages of Calradia Logistics,
   Refuges, Religions, Succession, or `AgesOfCalradiaSystems` modules; the
   combined manifests intentionally reject those duplicate identities.

Story Mode and MCM are optional. The release includes one Harmony runtime in
the Core folder; the two Systems folders use it through the Core dependency.

`CHECKSUMS-SHA256.txt` records every runtime file in the archive. Existing save
keys for Logistics, Refuges, Religions, Population, and Succession are retained.
